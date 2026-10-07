using UnityEngine;

namespace HorrorUtez.Remake
{
    // Automatic two-leaf glass door: opens for any living student or enemy close by, identically on every peer.
    // The leaves slide along this transform's local Z from the closed poses saved in the scene.
    public sealed class RemakeSlidingDoor : MonoBehaviour
    {
        public Transform LeafA, LeafB;
        [Tooltip("Distance each leaf travels when fully open.")]
        public float Travel = 1.85f;
        public float StudentRange = 4.5f, GiantRange = 6.5f;

        private Vector3 closedA, closedB;
        private float open;
        private bool wasOpen;
        private RemakeGame game;

        private void Start()
        {
            game = FindAnyObjectByType<RemakeGame>();
            if (LeafA != null) closedA = LeafA.localPosition;
            if (LeafB != null) closedB = LeafB.localPosition;
        }

        private void Update()
        {
            if (game == null || LeafA == null || LeafB == null) return;
            Vector3 centre = transform.position;
            bool near = false;
            foreach (StudentState s in game.Students.Values) if (s.alive && Flat(s.position - centre) < StudentRange) near = true;
            foreach (RemakeEnemy e in game.Enemies) if (Flat(e.transform.position - centre) < (e.Kind == EnemyKind.Giant ? GiantRange : StudentRange)) near = true;
            open = Mathf.MoveTowards(open, near ? 1 : 0, Time.deltaTime * 1.4f);
            if (near != wasOpen) { wasOpen = near; game.Audio?.Play("door_slide", centre + Vector3.up * 2, .8f, 18); }
            float e2 = Mathf.SmoothStep(0, 1, open);
            LeafA.localPosition = closedA + new Vector3(-.1f * e2, 0, e2 * Travel);
            LeafB.localPosition = closedB + new Vector3(-.1f * e2, 0, -e2 * Travel);
        }

        private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.5f, .9f, 1f);
            Gizmos.DrawWireSphere(transform.position, StudentRange);
        }
    }
}
