using UnityEngine;

namespace HorrorUtez.Remake
{
    // The shop is authored in remake_shop.unity (an additive scene at x=200). Session, voice and students remain in
    // the campus scene. Purchases are validated against the positions below, so move displays together with Product().
    public sealed class RemakeShop : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(200,0,0);
        public static Vector3 Spawn(int id) => Origin + new Vector3(-2+(id%3)*1.2f,.08f,-4+(id%2));
        public static Vector3 Product(int kind) => Origin + new Vector3(-5+kind*2,1.05f,1);
        public static readonly Vector3 Exit = Origin + new Vector3(0,0,-5.7f);
        public static bool NearProduct(Vector3 p,int kind) => Vector3.Distance(p,Product(kind))<2.3f;
        public static bool NearExit(Vector3 p) => Vector3.Distance(p,Exit)<2.5f;
        public static int AimedProduct(Camera view)
        {
            if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,2.6f)) {
                var product=hit.collider.GetComponent<RemakeShopProduct>();if(product!=null)return product.Kind;
            }
            return -1;
        }
        private void Awake() { transform.position=Origin; }
    }
}
