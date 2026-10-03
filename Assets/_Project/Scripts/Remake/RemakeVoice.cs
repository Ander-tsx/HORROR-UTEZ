using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorUtez.Remake
{
    // Experimental 8 kHz mono PCM proximity voice. No credentials, no cloud backend,
    // no recording to disk. Streaming playback keeps the network off the audio thread.
    public sealed class RemakeVoice : MonoBehaviour
    {
        private sealed class Speaker
        {
            public AudioSource source;
            public AudioClip clip;
            public readonly Queue<float> samples=new Queue<float>();
            public readonly object gate=new object();
            public float lastPacket;
        }
        private RemakeGame game;
        private AudioClip capture;
        private string device;
        private float nextPacket;
        private int readPosition;
        private readonly Dictionary<int,Speaker> speakers=new Dictionary<int,Speaker>();
        public bool Enabled { get; private set; }
        public bool TouchTalking { get; set; }
        public bool Talking { get; private set; }
        public string Status { get; private set; }="MIC APAGADO · MENÚ PARA ACTIVAR";
        public void Setup(RemakeGame owner)=>game=owner;
        public void SetEnabled(bool enabled)
        {
            Enabled=enabled;
            if(!enabled){StopMicrophone();return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                var callbacks=new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted+=_=>{if(Enabled)StartMicrophone();};
                callbacks.PermissionDenied+=_=>{Enabled=false;Status="MIC: PERMISO DENEGADO";};
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);
                Status="MIC: ESPERANDO PERMISO";return;
            }
#endif
            StartMicrophone();
        }
        private void StartMicrophone()
        {
            if(capture!=null)return;
            if(Microphone.devices.Length==0){Enabled=false;Status="NO HAY MICRÓFONO";return;}
            device=Microphone.devices[0];
            try
            {
                capture=Microphone.Start(device,true,2,8000);readPosition=0;
                if(capture==null){Enabled=false;Status="MIC NO DISPONIBLE";}
                else Status="V / VOZ: MANTENER PARA HABLAR";
            }
            catch(Exception e){Enabled=false;Status="MIC: "+e.Message;}
        }
        public void StopMicrophone()
        {
            if(device!=null)Microphone.End(device);
            if(capture!=null)Destroy(capture);capture=null;Talking=false;TouchTalking=false;
            if(!Enabled)Status="MIC APAGADO · MENÚ PARA ACTIVAR";
        }
        private void Update()
        {
            foreach(var pair in speakers)
            {
                if(game.Students.TryGetValue(pair.Key,out StudentState state))
                {pair.Value.source.transform.position=state.position+Vector3.up*1.4f;pair.Value.source.mute=!state.alive || game.Local==null || !game.Local.alive;}
                else pair.Value.source.mute=true;
            }
            if(capture==null)return;
            bool pressed=(Keyboard.current!=null && Keyboard.current.vKey.isPressed)||TouchTalking;
            Talking=Enabled && pressed && game.Started && game.Local!=null && game.Local.alive && !game.MenuOpen;
            Status=Talking?"● TRANSMITIENDO · TE PUEDEN OÍR":"V / VOZ: MANTENER PARA HABLAR";
            if(Time.unscaledTime<nextPacket)return;
            nextPacket=Time.unscaledTime+.1f;
            int write=Microphone.GetPosition(device);if(write<0)return;
            int available=(write-readPosition+capture.samples)%capture.samples;
            if(!Talking){readPosition=write;return;}
            int count=Mathf.Min(available,Mathf.RoundToInt(capture.frequency*.12f));
            if(count<160)return;
            float[] raw=new float[count*capture.channels];capture.GetData(raw,readPosition);
            readPosition=(readPosition+count)%capture.samples;
            int output=Mathf.Min(960,Mathf.RoundToInt((float)count*8000/capture.frequency));
            byte[] pcm=new byte[output*2];
            for(int i=0;i<output;i++)
            {
                int index=Mathf.Min(count-1,Mathf.FloorToInt((float)i*count/output))*capture.channels;
                short value=(short)Mathf.RoundToInt(Mathf.Clamp(raw[index],-1,1)*32767);
                pcm[i*2]=(byte)(value&255);pcm[i*2+1]=(byte)((value>>8)&255);
            }
            game.SendVoice(Convert.ToBase64String(pcm));
        }
        public void Receive(int id,string encoded)
        {
            if(id==game.LocalId || string.IsNullOrEmpty(encoded) || encoded.Length>11000)return;
            byte[] pcm;
            try{pcm=Convert.FromBase64String(encoded);}catch(FormatException){return;}
            if(pcm.Length>6400 || pcm.Length%2!=0)return;
            if(!speakers.TryGetValue(id,out Speaker speaker))
            {
                speaker=new Speaker();
                var go=new GameObject("Voz estudiante "+id);go.transform.SetParent(transform,false);
                speaker.source=go.AddComponent<AudioSource>();speaker.source.spatialBlend=1;
                speaker.source.rolloffMode=AudioRolloffMode.Linear;speaker.source.minDistance=1;speaker.source.maxDistance=20;
                speaker.source.volume=1;speaker.source.loop=true;
                Speaker target=speaker;
                speaker.clip=AudioClip.Create("Proximity voice "+id,16000,1,8000,true,buffer=>
                {
                    lock(target.gate)
                        for(int i=0;i<buffer.Length;i++)buffer[i]=target.samples.Count>0?target.samples.Dequeue():0;
                });
                speaker.source.clip=speaker.clip;speaker.source.Play();speakers[id]=speaker;
            }
            speaker.lastPacket=Time.unscaledTime;
            lock(speaker.gate)
            {
                if(speaker.samples.Count>4800)speaker.samples.Clear();
                for(int i=0;i<pcm.Length;i+=2)speaker.samples.Enqueue((short)(pcm[i]|pcm[i+1]<<8)/32768f);
            }
        }
        private void OnDestroy()
        {
            StopMicrophone();foreach(Speaker speaker in speakers.Values){speaker.source.Stop();Destroy(speaker.clip);}
        }
    }
}
