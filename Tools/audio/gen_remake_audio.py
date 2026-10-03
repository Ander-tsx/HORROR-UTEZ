"""Procedural music and sound effects for the remake (all original synthesis, no samples).

Run with any Python that has numpy, e.g. Blender's bundled one:
  blender -b --factory-startup --python Tools/audio/gen_remake_audio.py
Writes 16-bit mono WAVs to Assets/_Project/Art/Remake/Resources/Audio/<name>.wav,
loaded by RemakeAudio through Resources.Load<AudioClip>("Audio/<name>").
"""
import os
import struct
import sys

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Remake", "Resources", "Audio")
SR = 22050
rng = np.random.default_rng(1337)


def t_axis(sec):
    return np.arange(int(sec * SR)) / SR


def save(name, x, gain=0.9):
    x = np.asarray(x, dtype=np.float64)
    peak = np.max(np.abs(x)) + 1e-9
    x = x / peak * gain
    data = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, name + ".wav"), "wb") as f:
        f.write(b"RIFF" + struct.pack("<I", 36 + len(data)) + b"WAVEfmt " + struct.pack("<IHHIIHH", 16, 1, 1, SR, SR * 2, 2, 16))
        f.write(b"data" + struct.pack("<I", len(data)) + data)
    print("[audio]", name, f"{len(x) / SR:.1f}s")


def noise(sec):
    return rng.uniform(-1, 1, int(sec * SR))


def lowpass(x, cutoff):
    """One-pole low pass; cutoff may be an array (Hz) for sweeps."""
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = np.exp(-2 * np.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc = (1 - a[i]) * x[i] + a[i] * acc
        y[i] = acc
    return y


def lp(x, cutoff, order=2):
    for _ in range(order):
        x = lowpass(x, cutoff)
    return x


def hp(x, cutoff):
    return x - lowpass(x, cutoff)


def bandpass(x, f0, q):
    """RBJ biquad band-pass (constant peak gain)."""
    w = 2 * np.pi * f0 / SR
    alpha = np.sin(w) / (2 * q)
    b0, b1, b2 = alpha, 0, -alpha
    a0, a1, a2 = 1 + alpha, -2 * np.cos(w), 1 - alpha
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        y0 = (b0 * x[i] + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, x[i], y1, y0
        y[i] = y0
    return y


def env(n, attack, release, curve=3.0):
    t = np.arange(n) / SR
    e = np.minimum(1, t / max(attack, 1e-4))
    tail = np.clip((t - (n / SR - release)) / max(release, 1e-4), 0, 1)
    return e * (1 - tail) ** curve if release > 0 else e


def decay(n, time):
    return np.exp(-np.arange(n) / SR / time)


def saw(freq, sec, phase=0.0):
    t = t_axis(sec)
    f = np.broadcast_to(np.asarray(freq, dtype=float), t.shape)
    ph = np.cumsum(f) / SR + phase
    return 2 * (ph % 1) - 1


def sine(freq, sec, phase=0.0):
    t = t_axis(sec)
    f = np.broadcast_to(np.asarray(freq, dtype=float), t.shape)
    return np.sin(2 * np.pi * (np.cumsum(f) / SR + phase))


def reverb(x, time=2.5, mix=0.35, pre=0.02):
    n = int(time * SR)
    ir = rng.normal(0, 1, n) * decay(n, time / 6.9)
    ir = lp(ir, 3500, 1)
    ir[: int(pre * SR)] = 0
    ir /= np.sqrt(np.sum(ir ** 2))
    size = 1 << int(np.ceil(np.log2(len(x) + n)))
    wet = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[: len(x) + n]
    out = np.concatenate([x, np.zeros(n)]) * (1 - mix) + wet * mix * 2.2
    return out


def loopify(x, tail):
    """Fold a reverb tail back onto the start so a loop is seamless."""
    n = len(x) - tail
    y = x[:n].copy()
    y[:tail] += x[n:]
    fade = min(int(0.05 * SR), n // 4)
    ramp = np.linspace(0, 1, fade)
    y[:fade] = y[:fade] * ramp + y[n - fade:] * (1 - ramp) * 0
    return y


def place(buf, x, at):
    i = int(at * SR)
    end = min(len(buf), i + len(x))
    if i < len(buf):
        buf[i:end] += x[: end - i]


def metal_hit(sec, base, seed):
    r = np.random.default_rng(seed)
    out = np.zeros(int(sec * SR))
    for k in range(7):
        f = base * (1 + k * r.uniform(1.1, 1.9)) * r.uniform(0.97, 1.03)
        out += sine(f, sec) * decay(len(out), sec / (2 + k)) * r.uniform(0.3, 1) / (1 + k * 0.4)
    return out


# ---------------------------------------------------------------- music
def music_explore():
    sec = 48.0
    n = int(sec * SR)
    t = t_axis(sec)
    lfo = 0.5 + 0.5 * np.sin(2 * np.pi * t / 16)
    drone = (saw(55, sec) + saw(55.35, sec, 0.3) + 0.6 * saw(82.6, sec, 0.6) + 0.4 * saw(58.3, sec)) * 0.25
    drone = lp(drone, 180 + 420 * lfo, 2)
    sub = sine(27.5, sec) * (0.35 + 0.2 * np.sin(2 * np.pi * t / 6))
    hiss = lp(noise(sec), 2500, 1) * 0.02
    music = drone + sub + hiss
    for k in range(9):
        at = rng.uniform(1, sec - 6)
        swell = metal_hit(5, rng.choice([110, 146.8, 155.6, 207.6]), k) * env(int(5 * SR), 1.8, 0.0)
        place(music, swell * 0.25, at)
    for k in range(5):  # low piano-like clusters
        at = rng.uniform(2, sec - 4)
        tone = sum(sine(f, 4) for f in (rng.choice([65.4, 69.3, 98, 103.8]),) * 1)
        place(music, tone * decay(len(tone), 1.2) * 0.4, at)
    music = reverb(music, 4.0, 0.45)
    return loopify(music, int(4.0 * SR))


def music_chase():
    bpm = 150
    beat = 60 / bpm
    sec = beat * 32
    n = int(sec * SR)
    music = np.zeros(n)
    for b in range(32):
        at = b * beat
        k = int(0.5 * SR)
        kick = sine(np.linspace(140, 38, k), 0.5) * decay(k, 0.12) * 1.2
        place(music, kick, at)
        if b % 2 == 1:
            tom = sine(np.linspace(220, 90, k), 0.5) * decay(k, 0.09) * 0.6 + lp(noise(0.5), 1800) * decay(k, 0.03) * 0.6
            place(music, tom, at)
        if b % 4 == 2:
            place(music, metal_hit(0.8, 380, b) * 0.35, at + beat * 0.5)
        if b % 8 == 0:
            chord = (saw(110, beat * 4) + saw(116.5, beat * 4) + saw(155.6, beat * 4) + saw(164.8, beat * 4)) * 0.18
            chord = lp(chord, 900 + 600 * (b / 32), 1) * env(len(chord), 0.01, beat * 2, 1.5)
            place(music, chord, at)
    t = t_axis(sec)
    riser = lp(noise(sec), 300 + 2500 * (t / sec) ** 2, 1) * 0.08 * (t / sec)
    pulse = sine(55, sec) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * t / (beat / 2)))) * 0.15
    music += riser + pulse
    music = np.tanh(music * 1.6)
    return loopify(reverb(music, 1.5, 0.2), int(1.5 * SR))


def music_extract():
    sec = 7
    notes = [110, 138.6, 164.8, 207.6, 277.2]
    pad = sum(saw(f, sec, i * 0.2) + saw(f * 1.004, sec) for i, f in enumerate(notes)) * 0.12
    pad = lp(pad, 1400, 2) * env(len(pad), 1.5, 3.0, 2)
    bell = sum(sine(f * 4, sec) * decay(int(sec * SR), 1.5) * 0.2 for f in (110, 164.8))
    return reverb(pad + bell, 3.5, 0.4)


def music_fail():
    sec = 6
    x = (saw(41.2, sec) + saw(43.7, sec) + saw(61.7, sec) * 0.6) * 0.3
    x = lp(x, np.linspace(1200, 120, int(sec * SR)), 2) * env(int(sec * SR), 0.05, 4, 1.5)
    x += metal_hit(sec, 90, 77) * 0.6
    return reverb(x, 4, 0.5)


# ---------------------------------------------------------------- ambience
def amb_wind():
    sec = 20
    t = t_axis(sec)
    gust = 0.35 + 0.65 * (0.5 + 0.5 * np.sin(2 * np.pi * t / 7.3)) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 3.1 + 1))
    x = bandpass(noise(sec), 420, 0.8) * gust + bandpass(noise(sec), 1100, 2.5) * gust ** 2 * 0.4
    whistle = sine(780 + 90 * np.sin(2 * np.pi * t / 5), sec) * 0.02 * gust ** 3
    return loopify(np.concatenate([x + whistle, np.zeros(int(1 * SR))]), int(1 * SR))


def amb_fluoro():
    sec = 4
    t = t_axis(sec)
    hum = sum(sine(120 * k, sec) / k for k in range(1, 8)) * 0.3
    buzz = np.sign(sine(120, sec)) * 0.05
    crackle = (rng.random(len(t)) > 0.9993) * rng.uniform(-1, 1, len(t))
    crackle = lp(crackle, 4000, 1) * 6
    return hum + buzz + crackle


# ---------------------------------------------------------------- creatures
def formant_voice(sec, f0, formants, breath=0.4, growl=0.0):
    n = int(sec * SR)
    f0 = np.broadcast_to(np.asarray(f0, dtype=float), (n,))
    src = saw(f0, sec) * (1 - breath) + noise(sec) * breath
    if growl:
        src *= 1 + growl * sine(f0 * 0.5 + 23, sec)
    out = sum(bandpass(src, f, q) * g for f, q, g in formants)
    return out


def giant_breath():
    sec = 6
    n = int(sec * SR)
    t = t_axis(sec)
    cycle = np.sin(np.pi * (t % 3) / 3) ** 2
    inhale = t % 3 < 1.4
    x = bandpass(noise(sec), 500, 1.5) * cycle * np.where(inhale, 0.6, 1.0)
    x += formant_voice(sec, 48, [(300, 4, 1), (700, 5, 0.5)], 0.7, 0.6) * cycle * 0.7 * ~inhale
    return reverb(x, 1.2, 0.25)[:n]


def giant_groan():
    sec = 3
    f0 = np.linspace(70, 42, int(sec * SR)) * (1 + 0.03 * sine(5, sec))
    x = formant_voice(sec, f0, [(270, 5, 1), (680, 6, 0.6), (2300, 8, 0.15)], 0.3, 0.8)
    return reverb(np.tanh(x * 3) * env(len(x), 0.3, 1.2, 2), 2.5, 0.35)


def giant_scream():
    sec = 2.4
    n = int(sec * SR)
    f0 = np.concatenate([np.linspace(160, 420, n // 3), np.full(n - n // 3, 420.0)]) * (1 + 0.04 * sine(9, sec))
    hi = formant_voice(sec, f0, [(900, 3, 1), (2600, 5, 0.6), (3400, 6, 0.4)], 0.35, 0.3)
    lo = formant_voice(sec, f0 * 0.25, [(300, 3, 1)], 0.2, 1.0)
    x = np.tanh((hi + lo * 0.8) * 4) * env(n, 0.05, 1.0, 2)
    return reverb(x, 2.2, 0.3)


def giant_step():
    sec = 1.2
    n = int(sec * SR)
    thud = sine(np.linspace(70, 28, n), sec) * decay(n, 0.18)
    debris = lp(noise(sec), 2500, 1) * decay(n, 0.06) * 0.4
    rattle = metal_hit(sec, 520, 3) * decay(n, 0.15) * 0.08
    return np.tanh((thud + debris + rattle) * 2)


def whoosh(sec, f_from, f_to, gain=1.0):
    n = int(sec * SR)
    return bandpass(noise(sec), (f_from + f_to) / 2, 1.2) * np.sin(np.pi * np.linspace(0, 1, n)) ** 2 * gain


def giant_swipe():
    return whoosh(0.6, 300, 1200) + formant_voice(0.6, 90, [(400, 4, 1)], 0.5, 0.5) * env(int(0.6 * SR), 0.05, 0.4) * 0.6


def giant_hurt():
    sec = 1.6
    f0 = np.linspace(120, 55, int(sec * SR))
    return reverb(np.tanh(formant_voice(sec, f0, [(350, 4, 1), (900, 5, 0.5)], 0.3, 1.0) * 3) * env(int(sec * SR), 0.02, 1, 2), 1.5, 0.3)


def caretaker_hum():
    sec = 4
    f0 = 98 * (1 + 0.01 * sine(0.6, sec))
    x = formant_voice(sec, f0, [(300, 6, 1), (1900, 8, 0.2)], 0.65)
    return x * (0.6 + 0.4 * sine(0.25, sec))


def caretaker_alert():
    sec = 1.2
    n = int(sec * SR)
    trill = 2900 + 250 * np.sign(sine(28, sec))
    whistle = sine(trill, sec) * env(n, 0.01, 0.2) + bandpass(noise(sec), 2900, 4) * 0.3
    return reverb(whistle * 0.7, 1.0, 0.25)


def caretaker_swing():
    out = whoosh(0.5, 200, 900, 0.8)
    splat = lp(noise(0.25), 900)
    place(out, splat * decay(len(splat), 0.05), 0.25)
    return out


def caretaker_hurt():
    sec = 0.7
    return formant_voice(sec, np.linspace(180, 110, int(sec * SR)), [(500, 5, 1), (1500, 6, 0.4)], 0.3) * env(int(sec * SR), 0.01, 0.4)


def step_caretaker():
    n = int(0.25 * SR)
    return lp(noise(0.25), 1200, 2) * decay(n, 0.025) + sine(np.linspace(160, 60, n), 0.25) * decay(n, 0.03) * 0.5


# ---------------------------------------------------------------- player and world
def heartbeat():
    sec = 1.0
    n = int(sec * SR)
    out = np.zeros(n)
    for at, g in ((0.0, 1.0), (0.24, 0.7)):
        k = int(0.2 * SR)
        place(out, sine(np.linspace(60, 38, k), 0.2) * decay(k, 0.05) * g, at)
    return lp(out, 200, 1)


def breath_tired():
    sec = 2.2
    t = t_axis(sec)
    x = bandpass(noise(sec), 1300, 1.2) * (np.sin(np.pi * (t % 1.1) / 1.1) ** 3)
    return x


def hit_player():
    n = int(0.4 * SR)
    return np.tanh((sine(np.linspace(120, 45, n), 0.4) * decay(n, 0.08) + lp(noise(0.4), 3000) * decay(n, 0.02)) * 3)


def death_sting():
    sec = 4
    n = int(sec * SR)
    boom = sine(np.linspace(80, 25, n), sec) * decay(n, 0.6)
    screech = formant_voice(sec, np.linspace(800, 1300, n), [(2500, 3, 1), (3500, 4, 0.6)], 0.5) * env(n, 0.3, 2) * 0.4
    return reverb(np.tanh((boom + screech) * 2), 3, 0.4)


def glass_break():
    sec = 1.6
    n = int(sec * SR)
    out = hp(noise(sec), 2500) * decay(n, 0.08) * 0.5
    for k in range(30):
        at = rng.uniform(0, 0.9)
        ping = sine(rng.uniform(2500, 7000), 0.3) * decay(int(0.3 * SR), 0.04) * rng.uniform(0.1, 0.4)
        place(out, ping, at)
    out += sine(np.linspace(140, 50, n), sec) * decay(n, 0.05) * 0.6
    return reverb(out, 1.0, 0.2)


def impact_metal():
    return metal_hit(0.9, 260, 11) + lp(noise(0.9), 3000) * decay(int(0.9 * SR), 0.015) * 0.5


def impact_plastic():
    n = int(0.35 * SR)
    return lp(noise(0.35), 1600, 2) * decay(n, 0.03) + sine(np.linspace(300, 120, n), 0.35) * decay(n, 0.04) * 0.6


def value_lost():
    out = np.zeros(int(0.6 * SR))
    for i, f in enumerate((660, 520, 390)):
        k = int(0.14 * SR)
        place(out, np.sign(sine(f, 0.14)) * decay(k, 0.05) * 0.4, i * 0.12)
    return lp(out, 3000, 1)


def pickup():
    n = int(0.12 * SR)
    return lp(noise(0.12), 2000) * decay(n, 0.01) + sine(900, 0.12) * decay(n, 0.02) * 0.3


def ui_click():
    n = int(0.08 * SR)
    return sine(1400, 0.08) * decay(n, 0.01) + lp(noise(0.08), 4000) * decay(n, 0.004) * 0.4


def ui_hover():
    n = int(0.06 * SR)
    return sine(700, 0.06) * decay(n, 0.015) * 0.5


def cash():
    out = np.zeros(int(0.9 * SR))
    for i in range(6):
        place(out, metal_hit(0.5, rng.uniform(1800, 2600), 40 + i) * 0.4, i * 0.06)
    return out


def truck_engine():
    sec = 3
    t = t_axis(sec)
    fire = 24
    x = sum(sine(fire * k, sec) / (k ** 1.2) for k in range(1, 12)) * (1 + 0.3 * sine(fire / 2, sec))
    x = lp(np.tanh(x * 2) + lp(noise(sec), 400) * 0.3, 900, 1)
    return x


def truck_horn():
    sec = 1.2
    x = (saw(311, sec) + saw(392, sec)) * env(int(sec * SR), 0.02, 0.15, 1)
    return lp(np.tanh(x * 2), 2200, 2)


def door_creak():
    sec = 1.5
    f0 = 140 + 60 * np.sin(np.linspace(0, 9, int(sec * SR))) ** 2
    pulses = (np.sin(2 * np.pi * np.cumsum(f0) / SR) > 0.97).astype(float)
    return bandpass(pulses, 900, 3) * env(int(sec * SR), 0.1, 0.4)


def spark():
    sec = 0.7
    n = int(sec * SR)
    x = (rng.random(n) > 0.985) * rng.uniform(-1, 1, n)
    x = hp(x, 1500) * 4 + bandpass(noise(sec), 6000, 2) * 0.2
    return x * env(n, 0.005, 0.3)


def static_burst():
    sec = 0.8
    n = int(sec * SR)
    return hp(noise(sec), 800) * env(n, 0.01, 0.4) * (0.6 + 0.4 * np.sign(sine(60, sec)))


def whisper():
    sec = 3
    x = np.zeros(int(sec * SR))
    for k in range(6):
        at = rng.uniform(0, 2.2)
        syl = formant_voice(0.4, 0, [(rng.uniform(600, 1200), 6, 1), (rng.uniform(2000, 2800), 8, 0.6)], 1.0)
        place(x, syl * env(len(syl), 0.05, 0.25, 2), at)
    return reverb(hp(x, 300), 2.5, 0.5)


def distant_scream():
    sec = 2.5
    n = int(sec * SR)
    f0 = np.linspace(700, 520, n) * (1 + 0.03 * sine(6, sec))
    x = formant_voice(sec, f0, [(1000, 4, 1), (2700, 5, 0.5)], 0.3) * env(n, 0.1, 1.4, 2)
    return reverb(lp(x, 1800, 2), 4, 0.7)


def metal_bang():
    return reverb(metal_hit(2, 95, 21) + sine(np.linspace(90, 40, int(2 * SR)), 2) * decay(int(2 * SR), 0.2), 3.5, 0.6)


SOUNDS = {
    "music_explore": music_explore, "music_chase": music_chase, "music_extract": music_extract, "music_fail": music_fail,
    "amb_wind": amb_wind, "amb_fluoro": amb_fluoro,
    "giant_breath": giant_breath, "giant_groan": giant_groan, "giant_scream": giant_scream, "giant_step": giant_step,
    "giant_swipe": giant_swipe, "giant_hurt": giant_hurt,
    "caretaker_hum": caretaker_hum, "caretaker_alert": caretaker_alert, "caretaker_swing": caretaker_swing,
    "caretaker_hurt": caretaker_hurt, "step_caretaker": step_caretaker,
    "heartbeat": heartbeat, "breath_tired": breath_tired, "hit_player": hit_player, "death_sting": death_sting,
    "glass_break": glass_break, "impact_metal": impact_metal, "impact_plastic": impact_plastic, "value_lost": value_lost,
    "pickup": pickup, "ui_click": ui_click, "ui_hover": ui_hover, "cash": cash, "truck_engine": truck_engine,
    "truck_horn": truck_horn, "door_creak": door_creak, "spark": spark, "static_burst": static_burst,
    "whisper": whisper, "distant_scream": distant_scream, "metal_bang": metal_bang,
}

only = [a for a in sys.argv[sys.argv.index("--") + 1:]] if "--" in sys.argv else []
for name, fn in SOUNDS.items():
    if only and name not in only:
        continue
    save(name, fn(), 0.8 if name.startswith("music") or name.startswith("amb") else 0.95)
