using UnityEngine;

namespace HorrorUtez.Remake
{
    public static class RemakeBestiary
    {
        public static readonly string[] Names = { "Carsi", "Hugo", "Ulises", "Cristian", "Derick" };
        public static readonly string[] Entries = {
            "CARSI / EL OÍDO\nPequeño, encorvado y casi ciego. Oye pasos, conversaciones y golpes a mayor distancia.\nCamina agachado, habla poco y evita correr cerca de él. Las paredes amortiguan el sonido.",
            "HUGO / EL GIGANTE\nMuy alto; se pliega bajo techos. Lento al patrullar, fuerte al golpear y difícil de aturdir.\nSu golpe tiene una preparación larga. Sal de su alcance y rompe la línea de visión.",
            "ULISES / EL FOTÓFOBO\nReconoce linternas encendidas a gran distancia, pero ve mal en la oscuridad.\nApaga tu luz, agáchate y busca cobertura. Nunca detecta la luz a través de paredes.",
            "CRISTIAN / EL CUSTODIO\nVigila piezas valiosas del campus. Le llaman la atención los estudiantes que cargan botín.\nDeja el objeto, cambia de ruta y corta su visión. No conoce tu posición detrás de una pared.",
            "DERICK / EL CORREDOR\nAl verte se lanza en carreras de dos segundos, después necesita tres para recuperar el aliento.\nUsa esquinas durante su carrera y aprovecha su pausa. Sus golpes son menos fuertes."
        };
        public static float Chase(EnemyKind kind) => kind switch {
            EnemyKind.Giant => 3.05f, EnemyKind.Caretaker => 2.7f,
            EnemyKind.Ulises => 3.15f, EnemyKind.Cristian => 2.6f, _ => 4.4f };
        public static int Damage(EnemyKind kind) => kind switch {
            EnemyKind.Giant => 30, EnemyKind.Caretaker => 16, EnemyKind.Ulises => 14,
            EnemyKind.Cristian => 22, _ => 12 };
    }
}
