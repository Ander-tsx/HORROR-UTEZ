using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HorrorUtez.Remake
{
    public sealed class RemakeHud : MonoBehaviour
    {
        private RemakeGame game;
        private Font font;
        private RectTransform root;
        private GameObject menu, hud, pause, result, touch;
        private Text cargo, status, clock, vitals, prompt, roster, summary, connection, direction, mic;
        private Image cargoBar, staminaBar;
        private InputField nameField, addressField;
        private Button upgrade, next;
        private bool grab, interact, jump;
        private Vector2 look;
        public Vector2 MoveInput { get; private set; }
        public bool RunHeld { get; private set; }
        public bool GrabHeld { get; internal set; }
        public bool CrouchHeld { get; private set; }
        public bool MenuOpen => menu != null && (menu.activeSelf || pause.activeSelf || result.activeSelf);
        public bool TouchEnabled { get; private set; }
        private static readonly Color Bone=new Color(.87f,.86f,.77f), Teal=new Color(.36f,.87f,.7f), Red=new Color(.56f,.12f,.08f);

        public void Setup(RemakeGame owner)
        {
            game=owner;font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if(FindFirstObjectByType<EventSystem>()==null)
            {var es=new GameObject("Remake EventSystem");es.AddComponent<EventSystem>();es.AddComponent<InputSystemUIInputModule>();}
            var canvasGo=new GameObject("Remake UI");canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scale=canvasGo.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1600,900);scale.matchWidthOrHeight=.5f;
            canvasGo.AddComponent<GraphicRaycaster>();root=canvasGo.GetComponent<RectTransform>();
            menu=Panel("Menu principal",root,new Vector4(0,0,1,1),new Color(.01f,.025f,.025f,.22f));
            RectTransform pane=Panel("Hoja de turno",menu.transform,new Vector4(.055f,.075f,.46f,.93f),new Color(.025f,.045f,.042f,.95f)).GetComponent<RectTransform>();
            Label(pane,"UTEZ / RECUPERACIÓN DE EQUIPO",new Vector4(.06f,.89f,.94f,.96f),20,Teal);
            Label(pane,"TURNO\nNOCTURNO",new Vector4(.06f,.68f,.94f,.89f),64,Bone);
            Label(pane,"Los pasillos cerraron. La mudanza sigue.\nRecupera equipo sin romperlo. Vuelve al camión.",new Vector4(.06f,.59f,.94f,.68f),22,Bone);
            nameField=Field(pane,"Estudiante",new Vector4(.06f,.51f,.94f,.57f));
            Button(pane,"EXPLORAR SOLO",new Vector4(.06f,.42f,.49f,.49f),()=>game.Begin(false,nameField.text));
            Button(pane,"HOSPEDAR · 5",new Vector4(.51f,.42f,.94f,.49f),()=>game.Begin(true,nameField.text));
            addressField=Field(pane,"127.0.0.1",new Vector4(.06f,.33f,.66f,.39f));
            Button(pane,"UNIRME",new Vector4(.68f,.33f,.94f,.39f),()=>game.Join(addressField.text,nameField.text));
            Label(pane,"IP DEL ANFITRIÓN · PUERTO TCP 27777\nPC y Android en la misma red. Internet: puerto redirigido.",new Vector4(.06f,.25f,.94f,.32f),16,new Color(.57f,.66f,.6f));
            Button(pane,"MICRÓFONO: ACTIVAR",new Vector4(.06f,.16f,.63f,.23f),()=>ToggleMic());
            Button(pane,"SALIR",new Vector4(.65f,.16f,.94f,.23f),()=>Application.Quit());
            connection=Label(pane,"VERSIÓN EXPERIMENTAL / REMAKE",new Vector4(.06f,.04f,.94f,.14f),17,Teal);
            Label(menu.transform,"01\nEL CAMIÓN ES\nLA ÚNICA SALIDA",new Vector4(.64f,.65f,.94f,.92f),40,Bone,TextAnchor.UpperRight);
            Label(menu.transform,"COOPERACIÓN FÍSICA / BRAZOS ELÁSTICOS\nESTUDIANTES / CAMPUS UTEZ / URP",new Vector4(.6f,.1f,.94f,.22f),20,Teal,TextAnchor.LowerRight);

            hud=Panel("HUD",root,new Vector4(0,0,1,1),Color.clear,false);
            var top=Panel("Cuota",hud.transform,new Vector4(.025f,.88f,.365f,.975f),new Color(.025f,.045f,.042f,.9f));
            cargo=Label(top.transform,"",new Vector4(.05f,.32f,.95f,.91f),30,Teal);
            cargoBar=Panel("Progreso",top.transform,new Vector4(.05f,.14f,.95f,.23f),Teal,false).GetComponent<Image>();
            clock=Label(hud.transform,"",new Vector4(.73f,.89f,.97f,.97f),27,Bone,TextAnchor.UpperRight);
            roster=Label(hud.transform,"",new Vector4(.75f,.61f,.97f,.86f),20,Teal,TextAnchor.UpperRight);
            direction=Label(hud.transform,"",new Vector4(.39f,.91f,.71f,.97f),20,Teal,TextAnchor.MiddleCenter);
            Label(hud.transform,"+",new Vector4(.48f,.47f,.52f,.53f),28,Bone,TextAnchor.MiddleCenter);
            status=Label(hud.transform,"",new Vector4(.25f,.06f,.75f,.12f),19,Bone,TextAnchor.MiddleCenter);
            prompt=Label(hud.transform,"",new Vector4(.25f,.15f,.75f,.25f),23,Teal,TextAnchor.MiddleCenter);
            vitals=Label(hud.transform,"",new Vector4(.03f,.09f,.24f,.17f),22,Bone);
            staminaBar=Panel("Resistencia",hud.transform,new Vector4(.03f,.055f,.23f,.066f),Teal,false).GetComponent<Image>();
            mic=Label(hud.transform,"V: HABLAR",new Vector4(.78f,.035f,.97f,.09f),18,Teal,TextAnchor.LowerRight);
            Label(hud.transform,"WASD mover  ·  Shift correr  ·  Espacio saltar  ·  Ctrl/C agacharse (tras correr: barrida)\nMantén clic/G agarrar  ·  Rueda distancia  ·  R + ratón girar objeto  ·  E puertas/salir  ·  F luz  ·  Esc menú",new Vector4(.27f,.005f,.73f,.055f),14,new Color(.55f,.63f,.59f),TextAnchor.MiddleCenter);
            BuildTouch();
            pause=Panel("Pausa",root,new Vector4(.3f,.2f,.7f,.8f),new Color(.025f,.045f,.042f,.97f));
            Label(pause.transform,"TURNO EN CURSO",new Vector4(.08f,.78f,.92f,.95f),36,Bone);
            Label(pause.transform,"La partida online continúa mientras abres este menú.",new Vector4(.08f,.64f,.92f,.78f),19,Teal);
            Button(pause.transform,"VOLVER",new Vector4(.08f,.51f,.92f,.62f),()=>TogglePause());
            Button(pause.transform,"MICRÓFONO / PERMISO",new Vector4(.08f,.37f,.92f,.48f),()=>ToggleMic());
            Button(pause.transform,"CONTROLES TÁCTILES: MOSTRAR/OCULTAR",new Vector4(.08f,.23f,.92f,.34f),()=>{TouchEnabled=!TouchEnabled;touch.SetActive(TouchEnabled);});
            Button(pause.transform,"MENÚ PRINCIPAL",new Vector4(.08f,.09f,.92f,.2f),()=>SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            result=Panel("Resultado",root,new Vector4(.19f,.16f,.81f,.84f),new Color(.025f,.045f,.042f,.97f));
            Label(result.transform,"REPORTE DE RECUPERACIÓN",new Vector4(.06f,.8f,.94f,.94f),36,Teal);
            summary=Label(result.transform,"",new Vector4(.06f,.29f,.94f,.79f),25,Bone);
            upgrade=Button(result.transform,"FUERZA +1 / $80",new Vector4(.06f,.17f,.48f,.27f),()=>game.Request("upgrade"));
            next=Button(result.transform,"SIGUIENTE DÍA",new Vector4(.52f,.17f,.94f,.27f),()=>game.Request("next"));
            Button(result.transform,"MENÚ PRINCIPAL",new Vector4(.06f,.035f,.94f,.13f),()=>SceneManager.LoadScene(SceneManager.GetActiveScene().name));
            hud.SetActive(false);pause.SetActive(false);result.SetActive(false);
        }
        private void ToggleMic()
        {
            game.Voice.SetEnabled(!game.Voice.Enabled);
            SetConnectionStatus(game.Voice.Enabled?"Micrófono activo. Mantén V para hablar; en móvil, botón VOZ.":"Micrófono desactivado.");
        }
        public void SetConnectionStatus(string text){if(connection != null)connection.text=text;}
        public void ShowGame(){menu.SetActive(false);hud.SetActive(true);pause.SetActive(false);result.SetActive(false);SetCursor(false);}
        public void TogglePause()
        {
            if(menu.activeSelf || result.activeSelf)return;
            pause.SetActive(!pause.activeSelf);SetCursor(pause.activeSelf);MoveInput=Vector2.zero;look=Vector2.zero;RunHeld=false;
        }
        private void SetCursor(bool menuOn){Cursor.lockState=menuOn||TouchEnabled?CursorLockMode.None:CursorLockMode.Locked;Cursor.visible=menuOn||TouchEnabled;}
        public void Disconnected(string reason)
        {
            menu.SetActive(false);hud.SetActive(false);result.SetActive(true);summary.text=reason;
            upgrade.interactable=false;next.interactable=false;SetCursor(true);
        }
        private void Update()
        {
            if(game==null || !game.Started || game.Local==null)return;
            StudentState student=game.Local;
            if(game.Phase==3||game.Phase==4)
            {
                result.SetActive(true);pause.SetActive(false);SetCursor(true);
                string list="";
                foreach(StudentState s in game.Students.Values)list+=s.name+"  ·  "+(s.escaped?"EN EL CAMIÓN":"REVIVE SIN MEJORAS")+"\n";
                summary.text=game.Notice+"\n\n"+list+"\nTu saldo: $"+student.credits+"   /   Fuerza: "+student.strength+"/3";
                upgrade.interactable=game.Phase==3 && student.escaped && student.credits>=80 && student.strength<3;
                next.interactable=game.Authority;next.GetComponentInChildren<Text>().text=game.Authority?(game.Phase==4?"REINTENTAR":"SIGUIENTE DÍA"):"ESPERANDO AL HOST";
                return;
            }
            if(result.activeSelf){result.SetActive(false);SetCursor(false);}
            cargo.text="CARGA $"+game.Cargo+" / $"+game.Quota;
            cargoBar.rectTransform.anchorMax=new Vector2(.05f+.9f*Mathf.Clamp01((float)game.Cargo/game.Quota),.23f);
            clock.text="DÍA "+game.Day+"  /  "+(game.Seconds/60).ToString("00")+":"+(game.Seconds%60).ToString("00");
            roster.text="";foreach(StudentState s in game.Students.Values)roster.text+=s.name+"  "+(s.alive?s.health+" HP":"CAÍDO")+"\n";
            vitals.text=student.alive?"SALUD "+student.health+"   /   FUERZA "+student.strength:"CAÍDO · ESPERA LA EXTRACCIÓN";
            staminaBar.rectTransform.anchorMax=new Vector2(.03f+.2f*(game.Player!=null?game.Player.Stamina/100:1),.066f);
            status.text=game.Notice;
            if(game.Player!=null)
            {
                Vector3 delta=game.TruckPosition-game.Player.transform.position;
                float angle=Vector3.SignedAngle(game.Player.transform.forward,new Vector3(delta.x,0,delta.z),Vector3.up);
                direction.text=(Mathf.Abs(angle)<30?"↑":angle>0?"→":"←")+" CAMIÓN · "+Mathf.RoundToInt(delta.magnitude)+" m";
                RemakeLoot item=game.Player.AimedLoot;
                if(student.held>=0 && student.held<game.Loot.Count)
                {item=game.Loot[student.held];prompt.text=item.Label+" · "+item.Body.mass+" kg · $"+item.Value+"\nMantén clic/G para cargar · Suelta para dejarlo · Rueda: distancia · R: girar";}
                else if(game.InTruck(game.Player.transform.position))prompt.text=game.Cargo>=game.Quota?"[E / USAR] SALIR EN EL CAMIÓN":"Acomoda y suelta la carga. Faltan $"+(game.Quota-game.Cargo);
                else if(item!=null)prompt.text=item.Label+" · "+item.Body.mass+" kg · $"+item.Value+"\n[CLIC / AGARRAR] "+(item.Body.mass>24?"PESADO: pide ayuda":"Recuperar equipo");
                else if(game.Player.AimedDoor!=null)prompt.text="[E / USAR] ABRIR / CERRAR PUERTA";
                else prompt.text=student.alive?"":"Si un compañero salda la cuota y escapa, revives mañana.";
            }
            mic.text=game.Voice.Status;
        }
        private void BuildTouch()
        {
            touch=Panel("Controles móviles",hud.transform,new Vector4(0,0,1,1),Color.clear,false);
            var joystick=Panel("Mover",touch.transform,new Vector4(.015f,.02f,.235f,.36f),new Color(.08f,.16f,.13f,.28f));
            Label(joystick.transform,"MOVER",new Vector4(0,0,1,1),24,Teal,TextAnchor.MiddleCenter);
            joystick.AddComponent<RemakeTouchPad>().Setup(p=>MoveInput=Vector2.ClampMagnitude(p/(Screen.height*.12f),1),()=>MoveInput=Vector2.zero,false);
            var aim=Panel("Mirar",touch.transform,new Vector4(.35f,.28f,.99f,.87f),new Color(0,0,0,.001f));
            aim.AddComponent<RemakeTouchPad>().Setup(p=>look+=p*.09f,()=>{},true);
            HoldButton(touch.transform,"AGARRAR",new Vector4(.79f,.14f,.98f,.25f),held=>GrabHeld=held);
            Button(touch.transform,"USAR",new Vector4(.79f,.015f,.98f,.125f),()=>interact=true);
            Button(touch.transform,"SALTAR",new Vector4(.6f,.015f,.77f,.125f),()=>jump=true);
            Button(touch.transform,"LUZ",new Vector4(.6f,.14f,.77f,.25f),()=>game.Player?.ToggleTorch());
            Button(touch.transform,"MENÚ",new Vector4(.4f,.015f,.58f,.125f),()=>TogglePause());
            HoldButton(touch.transform,"CORRER",new Vector4(.25f,.14f,.42f,.25f),held=>RunHeld=held);
            HoldButton(touch.transform,"VOZ",new Vector4(.25f,.015f,.39f,.125f),held=>game.Voice.TouchTalking=held);
            TouchEnabled=Application.isMobilePlatform || Array.IndexOf(Environment.GetCommandLineArgs(),"-remakeTouch")>=0;
            touch.SetActive(TouchEnabled);
        }
        public bool ConsumeGrab(){bool value=grab;grab=false;return value;}
        public bool ConsumeInteract(){bool value=interact;interact=false;return value;}
        public bool ConsumeJump(){bool value=jump;jump=false;return value;}
        public Vector2 ConsumeLook(){Vector2 value=look;look=Vector2.zero;return value;}
        private GameObject Panel(string name,Transform parent,Vector4 area,Color color,bool raycast=true)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);Place(go.GetComponent<RectTransform>(),area);
            var image=go.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return go;
        }
        private Text Label(Transform parent,string text,Vector4 area,int size,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var go=new GameObject("Texto",typeof(RectTransform));go.transform.SetParent(parent,false);Place(go.GetComponent<RectTransform>(),area);
            var label=go.AddComponent<Text>();label.font=font;label.fontSize=size;label.text=text;label.color=color;label.alignment=alignment;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Overflow;label.raycastTarget=false;return label;
        }
        private Button Button(Transform parent,string label,Vector4 area,Action action)
        {
            GameObject go=Panel(label,parent,area,Red);var button=go.AddComponent<Button>();
            var colors=button.colors;colors.highlightedColor=new Color(1,.82f,.56f);colors.pressedColor=Teal;button.colors=colors;
            Label(go.transform,label,new Vector4(.03f,.05f,.97f,.95f),21,Bone,TextAnchor.MiddleCenter);
            button.onClick.AddListener(()=>action());return button;
        }
        private void HoldButton(Transform parent,string label,Vector4 area,Action<bool> action)
        {
            Button button=Button(parent,label,area,()=>{});button.gameObject.AddComponent<RemakeHoldButton>().Changed=action;
        }
        private InputField Field(Transform parent,string value,Vector4 area)
        {
            GameObject go=Panel("Entrada",parent,area,new Color(.08f,.15f,.13f));
            Text text=Label(go.transform,value,new Vector4(.04f,0,.96f,1),23,Bone);
            var field=go.AddComponent<InputField>();field.textComponent=text;field.text=value;field.characterLimit=64;return field;
        }
        private static void Place(RectTransform rect,Vector4 area)
        {rect.anchorMin=new Vector2(area.x,area.y);rect.anchorMax=new Vector2(area.z,area.w);rect.offsetMin=rect.offsetMax=Vector2.zero;}
    }
    public sealed class RemakeTouchPad : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        private Action<Vector2> moved;private Action released;private bool delta;private Vector2 origin;private int pointer=int.MinValue;
        public void Setup(Action<Vector2> move,Action release,bool look){moved=move;released=release;delta=look;}
        public void OnPointerDown(PointerEventData e){if(pointer!=int.MinValue)return;pointer=e.pointerId;origin=e.position;}
        public void OnDrag(PointerEventData e){if(e.pointerId==pointer)moved(delta?e.delta:e.position-origin);}
        public void OnPointerUp(PointerEventData e){if(e.pointerId!=pointer)return;pointer=int.MinValue;released();}
        private void OnDisable(){pointer=int.MinValue;released?.Invoke();}
    }
    public sealed class RemakeHoldButton : MonoBehaviour,IPointerDownHandler,IPointerUpHandler
    {
        public Action<bool> Changed;
        public void OnPointerDown(PointerEventData e)=>Changed?.Invoke(true);
        public void OnPointerUp(PointerEventData e)=>Changed?.Invoke(false);
        private void OnDisable()=>Changed?.Invoke(false);
    }
}
