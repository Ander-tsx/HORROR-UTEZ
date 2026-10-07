using System.Collections.Generic;
using HorrorUtez.Core;
using HorrorUtez.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Remake
{
    public sealed class RemakeStudent : MonoBehaviour
    {
        // Feel modelled on R.E.P.O.'s PlayerController: friction-smoothed
        // velocity, sprint ramp that drains energy, crouch-slide after sprinting, coyote/jump buffers,
        // heavier gravity and weighted landings.
        public const float MinReach = .75f, MaxReach = 1.6f, BaseEnergy = 40;
        // Upgrades (StudentState) raise these caps the same way on the host and the owner.
        public static float ReachCap(StudentState s) => MaxReach + .25f * (s?.range ?? 0);
        public float EnergyMax => BaseEnergy + 10 * (game?.Local?.stamina ?? 0);
        private float SprintCap => SprintSpeed + .45f * (game?.Local?.speed ?? 0);
        private const float WalkSpeed = 2.6f, SprintSpeed = 5.4f, CrouchSpeed = 1.3f, Friction = 10, AirFriction = 2.2f;
        private const float SprintRamp = 1.4f, SprintDrain = 4.5f, EnergyRecharge = 3, Gravity = 18, JumpSpeed = 5.6f;
        // Students are a little under real size (1.5 m) so the campus and the giant loom larger.
        private const float StandHeight = 1.5f, CrouchHeight = .92f, StandEye = 1.38f, CrouchEye = .82f, SlideTime = .85f;
        public const float AvatarScale = .88f;

        public Camera View => rig?.View;
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Reach { get; private set; } = 1.2f;
        public Quaternion Hold { get; private set; } = Quaternion.identity;
        public float EyeHeight { get; private set; } = StandEye;
        public float Speed { get; private set; }
        public float Energy { get; private set; } = BaseEnergy;
        public float Stamina => Energy / EnergyMax * 100;
        public bool Running { get; private set; }
        public bool Crouching { get; private set; }
        public bool Crawling { get; private set; }
        public bool Sliding { get; private set; }
        public bool Rotating { get; private set; }
        public bool Tumbling { get; private set; }
        private Rigidbody tumbleBody;
        private Vector3 lastTumbleVelocity, stillPoint;
        private float tumbleLock, stillTimer;
        public bool TorchOn => torch != null && torch.enabled;
        public bool LocalPlayer { get; private set; }
        public int Id { get; private set; }
        private RemakeGame game;
        private CharacterController controller;
        private Transform eyes;
        private RemakeCameraRig rig;
        private Light torch;
        private Animator animator;
        private Quaternion neutralHeadRotation=Quaternion.identity;
        private int appliedSkin=-1;
        private Vector3 visualScale = Vector3.one, previous, planar, slideVelocity, aimedPoint, pullPoint;
        private int airJumps, spectateIndex;
        public string Spectating { get; private set; }
        private float vertical, sprintLerp, sprintedTimer, rechargeDelay, crouchTimer, slideTimer, groundBuffer, jumpBuffer, jumpCooldown;
        private float fallSpeed, nextRemoteStep, scanAt, nextScare, pullTimer;
        private bool wasGrounded = true, previousGrip;
        private int lastHealth = 100;
        private float nextGrabAttempt;
        private readonly Dictionary<RemakeEnemy, float> enemyDistance = new Dictionary<RemakeEnemy, float>();
        private StudentState remote;
        private GameObject visual;
        private Transform[,] arms = new Transform[2, 3];
        private readonly RemakeHandRig[] hands=new RemakeHandRig[2];
        private AudioSource footsteps;
        private AudioClip step, scare;
        private TextMesh nameplate;
        public RemakeLoot AimedLoot { get; private set; }
        public HingedDoor AimedDoor { get; private set; }

        public void Setup(RemakeGame owner, int id, bool local)
        {
            game = owner; Id = id; LocalPlayer = local; gameObject.layer = 9;
            transform.position = game.Students.TryGetValue(id, out StudentState state) ? state.position : game.SpawnPoint(id);
            previous = transform.position;
            if (local)
            {
                controller = gameObject.AddComponent<CharacterController>(); controller.height = StandHeight;
                controller.radius = .25f; controller.center = Vector3.up*(StandHeight*.5f); controller.stepOffset = .3f; controller.slopeLimit = 48;
                eyes = new GameObject("Eyes").transform; eyes.SetParent(transform, false); eyes.localPosition = Vector3.up*StandEye;
                rig = new RemakeCameraRig(eyes); rig.Step = Footstep;
                View.nearClipPlane=.04f; View.farClipPlane=180; View.fieldOfView=rig.BaseFov;
                View.tag="MainCamera"; View.allowHDR=true; View.backgroundColor=new Color(.015f,.035f,.035f);
                View.clearFlags=CameraClearFlags.SolidColor;
                var data = View.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true;
                View.gameObject.AddComponent<AudioListener>();
                scare = RemakeSound.Tone("scare sting", 52, .9f, .55f);
            }
            else if(game.StudentModel != null)
            {
                visual = Instantiate(game.StudentModel, transform); visual.transform.localScale*=AvatarScale; visualScale=visual.transform.localScale;
                foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled=false;
                animator = visual.GetComponentInChildren<Animator>();
                if(animator != null) {
                    animator.applyRootMotion=false;
                    if(animator.isHuman) {
                        var head=animator.GetBoneTransform(HumanBodyBones.Head);
                        if(head!=null)neutralHeadRotation=Quaternion.Inverse(transform.rotation)*head.rotation;
                    }
                }
                var label = new GameObject("Nombre"); label.transform.SetParent(transform,false); label.transform.localPosition=Vector3.up*2;
                nameplate=label.AddComponent<TextMesh>(); nameplate.anchor=TextAnchor.MiddleCenter; nameplate.fontSize=40;
                nameplate.characterSize=.026f; nameplate.color=new Color(.7f,.9f,.78f);
            }
            // The torch hangs from the smoothed aim, not the bobbing camera, so its beam sways less than the view.
            var lightGo = new GameObject("Linterna"); lightGo.transform.SetParent(local ? rig.Aim : transform,false);
            lightGo.transform.localPosition=local ? new Vector3(.18f,-.12f,.08f) : new Vector3(.22f,1.4f,.16f);
            torch=lightGo.AddComponent<Light>(); torch.type=LightType.Spot; torch.range=24; torch.spotAngle=68;
            torch.innerSpotAngle=28; torch.intensity=35; torch.color=new Color(.85f,.95f,1);
            torch.shadows=local && !Application.isMobilePlatform ? LightShadows.Soft : LightShadows.None;
            torch.shadowResolution=UnityEngine.Rendering.LightShadowResolution.Low;
            torch.cullingMask &= ~(1<<8);
            if(local) {
                var fill=new GameObject("Reflejo tenue / manos");fill.transform.SetParent(View.transform,false);
                var handLight=fill.AddComponent<Light>();handLight.type=LightType.Point;handLight.range=2.5f;
                handLight.intensity=.2f;handLight.color=new Color(.9f,.83f,.76f);handLight.cullingMask=1<<8;
                handLight.shadows=LightShadows.None;
            }
            footsteps = gameObject.AddComponent<AudioSource>(); footsteps.spatialBlend = 1; footsteps.volume=.14f;
            footsteps.rolloffMode=AudioRolloffMode.Linear; footsteps.maxDistance=18;
            step=ProceduralAudio.CreateFootstep(id+106,false);
            for(int side=0;side<2;side++)
            {
                for(int segment=0;segment<2;segment++)
                {
                    var limb=GameObject.CreatePrimitive(PrimitiveType.Capsule);Destroy(limb.GetComponent<Collider>());
                    limb.name=segment==0?"Manga delgada":"Antebrazo delgado";limb.layer=8;limb.transform.SetParent(transform,false);
                    limb.GetComponent<Renderer>().sharedMaterial=segment==0?game.SleeveMaterial:game.SkinMaterial;
                    arms[side,segment]=limb.transform;
                }
                hands[side]=new RemakeHandRig(transform,side,game.SkinMaterial);arms[side,2]=hands[side].Root;
            }
            ApplySkin(state?.skin??1);
        }

        private void ApplySkin(int skin) {
            skin=RemakeSkins.Clamp(skin);if(appliedSkin==skin)return;appliedSkin=skin;
            if(visual!=null)RemakeSkins.Apply(visual,skin);
            for(int side=0;side<2;side++) {
                hands[side].SetAppearance(RemakeSkins.SkinColor(skin));
                RemakeSkins.Tint(arms[side,0].GetComponent<Renderer>(),RemakeSkins.ShirtColor(skin));
                RemakeSkins.Tint(arms[side,1].GetComponent<Renderer>(),RemakeSkins.SkinColor(skin));
            }
        }

        public void Apply(StudentState state) { remote=state; }
        public void ToggleTorch() { if(torch!=null)torch.enabled=!torch.enabled; }
        internal void Carry(Vector3 delta) {
            if(controller!=null)controller.enabled=false;
            transform.position+=delta;
            if(controller!=null)controller.enabled=true;
        }
        public void Teleport(Vector3 position,float pitch=0)
        {
            if(controller != null)controller.enabled=false;
            transform.SetPositionAndRotation(position,Quaternion.identity);
            if(controller != null)controller.enabled=true;
            if(Tumbling){tumbleBody.gameObject.SetActive(false);Tumbling=false;}
            vertical=0; planar=Vector3.zero; Yaw=0; Pitch=pitch; Energy=EnergyMax; previous=position;
            Sliding=false; fallSpeed=0; wasGrounded=true; pullTimer=0;
            if(rig != null)rig.Aim.rotation=Quaternion.Euler(Pitch,Yaw,0);
        }
        private void Footstep(float loudness)
        {
            footsteps.pitch=Random.Range(.9f,1.1f); footsteps.PlayOneShot(step,loudness);
        }
        private void Update()
        {
            if(game == null || !game.Started)return;
            if(torch!=null)torch.intensity=game.Phase==3?1.8f:8;
            if(!LocalPlayer)
            {
                if(remote == null)return;
                transform.position=Vector3.Lerp(transform.position,remote.position,Time.deltaTime*16);
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,remote.yaw,0),Time.deltaTime*16);
                if(visual != null)
                {
                    visual.transform.localRotation=Quaternion.Slerp(visual.transform.localRotation,Quaternion.Euler(!remote.alive ? 85 : remote.tumble ? 75 : 0,0,0),Time.deltaTime*10);
                    visual.transform.localScale=visualScale;
                }
                torch.enabled=remote.alive && remote.torch; torch.transform.rotation=Quaternion.LookRotation(remote.aim);
                if(nameplate != null)
                {
                    nameplate.text=remote.name + (remote.alive ? "" : " · CAÍDO");
                    if(game.Player != null)nameplate.transform.rotation=Quaternion.LookRotation(nameplate.transform.position-game.Player.View.transform.position);
                }
                Vector3 velocity=(transform.position-previous)/Mathf.Max(Time.deltaTime,.001f);
                Speed=velocity.magnitude;
                if(animator != null && animator.runtimeAnimatorController != null)
                {
                    Vector3 local=transform.InverseTransformDirection(velocity);
                    animator.SetFloat("MoveX",local.x,.15f,Time.deltaTime); animator.SetFloat("MoveZ",local.z,.15f,Time.deltaTime);
                    animator.SetFloat("Speed",Speed); animator.SetBool("Grounded",true);
                    animator.SetFloat("Crouch",remote.eye<1.2f?1:0,.18f,Time.deltaTime);
                    animator.SetFloat("VerticalSpeed",velocity.y,.15f,Time.deltaTime);
                }
                if(remote.alive && Speed>1 && Time.time>nextRemoteStep){Footstep(Speed>4?1:.6f);nextRemoteStep=Time.time+(Speed>4?.38f:.55f);}
                previous=transform.position;return;
            }
            if(game.Local == null)return;
            float dt=Time.deltaTime;
            if(game.MenuOpen || !game.CanMove)
            {
                // Held grip requires the button; menus/phase changes must not leave objects stuck in the hand.
                if(game.Local.held>=0)game.Request("drop");
                previousGrip=false; Rotating=false; Running=false; Speed=0;
                if(Tumbling)transform.position=tumbleBody.position-Vector3.up*.6f;
                rig.Tick(Yaw,Pitch,true,0,false,0,Crouching,0,dt);
                return;
            }
            // Automated runs (smoke, tour, watch) ignore real devices so someone using the PC cannot steer the test.
            var keyboard=game.Automated ? null : Keyboard.current; var mouse=game.Automated ? null : Mouse.current; var pad=game.Automated ? null : Gamepad.current;
            Vector2 move=game.Hud.MoveInput;
            Vector2 look=game.Hud.ConsumeLook();
            bool run=game.Hud.RunHeld, jump=game.Hud.ConsumeJump(), grab=game.Hud.GrabHeld, interact=game.Hud.ConsumeInteract();
            bool crouch=game.Hud.CrouchHeld, rotate=false, tumble=false;
            float scroll=0;
            if(keyboard != null)
            {
                move+=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
                run|=keyboard.leftShiftKey.isPressed; jump|=keyboard.spaceKey.wasPressedThisFrame;
                grab|=keyboard.gKey.isPressed; interact|=keyboard.eKey.wasPressedThisFrame;
                crouch|=keyboard.leftCtrlKey.isPressed||keyboard.cKey.isPressed; rotate|=keyboard.rKey.isPressed;
                if(keyboard.fKey.wasPressedThisFrame)torch.enabled=!torch.enabled;
                tumble|=keyboard.qKey.wasPressedThisFrame;
            }
            if(mouse != null)
            {
                look+=mouse.delta.ReadValue()*.1f;
                grab|=mouse.leftButton.isPressed;
                scroll=mouse.scroll.ReadValue().y;
                if(mouse.rightButton.isPressed)Reach=Mathf.MoveTowards(Reach,1.2f,dt*2);
            }
            if(pad != null)
            {
                move+=pad.leftStick.ReadValue(); look+=pad.rightStick.ReadValue()*dt*160;
                run|=pad.leftStickButton.isPressed; jump|=pad.buttonSouth.wasPressedThisFrame;
                grab|=pad.rightShoulder.isPressed; interact|=pad.buttonWest.wasPressedThisFrame;
                crouch|=pad.buttonEast.isPressed; rotate|=pad.leftShoulder.isPressed; tumble|=pad.buttonNorth.wasPressedThisFrame;
                if(pad.dpad.up.wasPressedThisFrame)scroll=1; if(pad.dpad.down.wasPressedThisFrame)scroll=-1;
            }
            bool holding=game.Local.held>=0;
            Rotating=rotate && holding && game.Local.alive;
            if(Rotating)
            {
                // Rotate mode: the mouse turns the held object in camera space instead of the view.
                Hold=Quaternion.AngleAxis(look.x*.9f,Vector3.up)*Quaternion.AngleAxis(look.y*.9f,Vector3.right)*Hold;
            }
            else
            {
                Yaw+=look.x*GameSettings.LookSensitivity;
                Pitch=Mathf.Clamp(Pitch-look.y*GameSettings.LookSensitivity,-70,80);
            }
            if(holding && !Rotating && scroll != 0)Reach=Mathf.Clamp(Reach+Mathf.Sign(scroll)*.2f,MinReach,ReachCap(game.Local));
            ScanEnemies(dt);
            transform.rotation=Quaternion.Euler(0,Yaw,0);
            bool grounded=true;
            if(game.Local.alive)
            {
                if(game.Local.health<lastHealth)
                {
                    rig.Hit(Mathf.Clamp01((lastHealth-game.Local.health)/30f));
                    // Enemy blows carry a launch velocity from the host and knock the student over (R.E.P.O.-style tumble).
                    Vector3 knock=game.Local.knock;
                    if(knock.sqrMagnitude>.01f)
                    {
                        if(Tumbling)tumbleBody.AddForce(knock,ForceMode.VelocityChange);
                        else StartTumble(knock,1.2f);
                        tumbleLock=Mathf.Max(tumbleLock,1.2f);
                    }
                }
                if(Tumbling)grounded=TumbleUpdate(move,jump||tumble,dt);
                else if(tumble)StartTumble(planar+Vector3.up*Mathf.Max(vertical,0),0);
                else grounded=Move(move,run,jump,crouch,dt);
                if(Tumbling)
                {
                    // Tumbling bodies cannot hold or use anything.
                    AimedLoot=null; AimedDoor=null; grab=false; interact=false;
                }
                else Probe();
                if(grab && !holding && Time.unscaledTime>nextGrabAttempt && AimedLoot!=null)
                {
                    // Grab exactly where the ray hit and keep the object's orientation relative to the camera.
                    Quaternion cam=Quaternion.Euler(Pitch,Yaw,0);
                    Hold=Quaternion.Inverse(cam)*AimedLoot.transform.rotation;
                    Reach=Mathf.Clamp(Vector3.Distance(EyePosition,aimedPoint),MinReach,ReachCap(game.Local));
                    game.Request("grab",AimedLoot.Id,AimedLoot.transform.InverseTransformPoint(aimedPoint),Hold);
                    nextGrabAttempt=Time.unscaledTime+.15f;
                }
                if(!grab && (previousGrip || holding))game.Request("drop");
                previousGrip=grab;
                if(interact)
                {
                    if(game.Phase==3)game.ShopInteract();
                    else if(game.InTruck(transform.position))game.Request("extract");
                    else if(AimedDoor != null)game.Request("door",game.DoorId(AimedDoor));
                }
            }
            else
            {
                if(Tumbling)EndTumble();
                Speed=0; torch.enabled=false; Running=false; Sliding=false;
                // Fallen students spectate a living teammate (click to switch), like R.E.P.O.'s spectator camera,
                // until someone carries their ID card into the truck.
                StudentState watch=null; int alive=0;
                foreach(StudentState s in game.Students.Values) if(s.alive && s.id!=game.LocalId) alive++;
                if(alive>0)
                {
                    bool next=(mouse!=null && mouse.leftButton.wasPressedThisFrame) || jump;
                    if(next)spectateIndex++;
                    int k=0, pick=spectateIndex%alive;
                    foreach(StudentState s in game.Students.Values) if(s.alive && s.id!=game.LocalId){ if(k==pick)watch=s; k++; }
                }
                Transform target=watch!=null ? game.Avatar(watch.id) : null;
                Spectating=target!=null ? watch.name : null;
                if(target!=null)
                {
                    controller.enabled=false;
                    Vector3 focus=target.position+Vector3.up*1.1f;
                    Vector3 cam=focus-Quaternion.Euler(0,watch.yaw,0)*Vector3.forward*2.6f+Vector3.up*.7f;
                    EyeHeight=Mathf.Lerp(EyeHeight,1.2f,dt*4); eyes.localPosition=Vector3.up*EyeHeight;
                    transform.position=Vector3.Lerp(transform.position,cam-Vector3.up*EyeHeight,1-Mathf.Exp(-6*dt));
                    Vector3 d=focus-(transform.position+Vector3.up*EyeHeight);
                    Yaw=Mathf.LerpAngle(Yaw,Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,1-Mathf.Exp(-8*dt));
                    Pitch=Mathf.Lerp(Pitch,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg,1-Mathf.Exp(-8*dt));
                    transform.rotation=Quaternion.Euler(0,Yaw,0);
                }
                else { EyeHeight=Mathf.Lerp(EyeHeight,.38f,dt*4); eyes.localPosition=Vector3.up*EyeHeight; }
            }
            lastHealth=game.Local.health;
            rig.Tick(Yaw,Pitch,grounded,Speed,Running,sprintLerp,Crouching,move.x,dt);
        }
        // Tumble (R.E.P.O.-style ragdoll): the student becomes a rolling physics capsule that keeps its momentum.
        // The owner simulates it like normal movement; other students cannot grab it yet.
        private void StartTumble(Vector3 velocity,float lockTime)
        {
            if(tumbleBody==null)
            {
                var go=new GameObject("Estudiante rodando"); go.layer=9;
                tumbleBody=go.AddComponent<Rigidbody>(); tumbleBody.mass=60; tumbleBody.linearDamping=.15f; tumbleBody.angularDamping=1.2f;
                tumbleBody.interpolation=RigidbodyInterpolation.Interpolate; tumbleBody.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                var capsule=go.AddComponent<CapsuleCollider>(); capsule.radius=.3f; capsule.height=1.1f;
            }
            controller.enabled=false;
            tumbleBody.gameObject.SetActive(true);
            tumbleBody.position=transform.position+Vector3.up*.6f; tumbleBody.rotation=transform.rotation;
            tumbleBody.transform.SetPositionAndRotation(tumbleBody.position,tumbleBody.rotation);
            tumbleBody.linearVelocity=velocity; lastTumbleVelocity=velocity;
            Vector3 spin=Vector3.Cross(Vector3.up,velocity);
            tumbleBody.angularVelocity=(spin.sqrMagnitude>.01f ? spin.normalized : Random.onUnitSphere)*2;
            Tumbling=true; tumbleLock=lockTime; stillTimer=0; stillPoint=tumbleBody.position;
            Crouching=false; Running=false; Sliding=false; planar=Vector3.zero; vertical=0; sprintLerp=0;
            Footstep(1); rig.Shake(1.2f,.3f);
        }
        public void Tumble(Vector3 velocity) { if(!Tumbling && controller!=null)StartTumble(velocity,0); }
        public void GetUp() { if(Tumbling)EndTumble(); }
        public void Shake(float degrees,float seconds) { rig?.Shake(degrees,seconds); }
        public void Aim(float yaw,float pitch) { Yaw=yaw; Pitch=Mathf.Clamp(pitch,-70,80); transform.rotation=Quaternion.Euler(0,Yaw,0); if(rig!=null)rig.Aim.rotation=Quaternion.Euler(Pitch,Yaw,0); }
        private bool TumbleUpdate(Vector2 move,bool getUp,float dt)
        {
            tumbleLock-=dt;
            transform.position=tumbleBody.position-Vector3.up*.6f;
            EyeHeight=Mathf.Lerp(EyeHeight,.75f,1-Mathf.Exp(-10*dt)); eyes.localPosition=Vector3.up*EyeHeight;
            Vector3 velocity=tumbleBody.linearVelocity;
            float impact=(velocity-lastTumbleVelocity).magnitude; lastTumbleVelocity=velocity;
            if(impact>4.5f)
            {
                float strength=Mathf.Clamp01((impact-4.5f)/8);
                rig.Land(strength); Footstep(1); game.Noise(transform.position,8+strength*14);
            }
            if((tumbleBody.position-stillPoint).magnitude>.5f){stillPoint=tumbleBody.position;stillTimer=0;} else stillTimer+=dt;
            rig.Zoom(-12,.1f);
            Speed=velocity.magnitude;
            if(tumbleLock<=0 && (getUp || (move.sqrMagnitude>.04f && stillTimer>.5f)))EndTumble();
            return false;
        }
        private void EndTumble()
        {
            Vector3 point=tumbleBody.position;
            if(Physics.Raycast(point+Vector3.up*.3f,Vector3.down,out RaycastHit ground,2.5f,game.WorldMask,QueryTriggerInteraction.Ignore))point=ground.point;
            else point-=Vector3.up*.6f;
            planar=Vector3.ProjectOnPlane(tumbleBody.linearVelocity,Vector3.up)*.3f;
            tumbleBody.gameObject.SetActive(false); Tumbling=false;
            transform.position=point+Vector3.up*.02f; controller.enabled=true; vertical=0; wasGrounded=true;
            // Like a revive in R.E.P.O., the student gets up crouched and stands once there is room.
            Crouching=true; crouchTimer=.2f; rig.Jump();
        }
        private void FixedUpdate()
        {
            if(Tumbling)tumbleBody.AddForce(Vector3.down*8,ForceMode.Acceleration);
        }
        private void OnDestroy()
        {
            if(tumbleBody!=null)Destroy(tumbleBody.gameObject);
        }
        public Vector3 EyePosition => transform.position+Vector3.up*EyeHeight;
        private bool Move(Vector2 move,bool run,bool jump,bool crouch,float dt)
        {
            bool grounded=controller.isGrounded || Physics.Raycast(transform.position+Vector3.up*.2f,Vector3.down,.32f,game.WorldMask,QueryTriggerInteraction.Ignore);
            groundBuffer=grounded ? .25f : groundBuffer-dt;
            jumpBuffer=jump ? .25f : jumpBuffer-dt;
            jumpCooldown-=dt;

            // Crouch has a minimum duration; a low ceiling keeps the student crawling until there is room.
            Crawling=Crouching && Physics.SphereCast(transform.position+Vector3.up*(CrouchHeight-.3f),.24f,Vector3.up,out _,
                StandHeight-CrouchHeight+.08f,game.WorldMask,QueryTriggerInteraction.Ignore);
            if(crouch){ if(!Crouching){Crouching=true;crouchTimer=.2f;} }
            else if(Crouching && crouchTimer<=0 && !Crawling)Crouching=false;
            crouchTimer-=dt;

            Vector3 wish=transform.TransformDirection(new Vector3(move.x,0,move.y));
            if(wish.sqrMagnitude>1)wish.Normalize();
            if(run && !Crouching && Energy>0 && wish.sqrMagnitude>.04f)
            {
                Running=true; sprintLerp=Mathf.Clamp01(sprintLerp+SprintRamp*dt); sprintedTimer=.5f;
                Energy=Mathf.Max(0,Energy-SprintDrain*sprintLerp*dt); rechargeDelay=1;
            }
            else
            {
                Running=false; sprintLerp=0; sprintedTimer-=dt;
                if(rechargeDelay>0)rechargeDelay-=dt; else Energy=Mathf.Min(EnergyMax,Energy+EnergyRecharge*dt);
            }
            // Crouching right after a sprint turns momentum into a slide that costs a little energy.
            if(Crouching && sprintedTimer>0 && !Sliding && grounded && planar.magnitude>3.5f && Energy>=2)
            {
                Sliding=true; slideTimer=SlideTime; slideVelocity=planar*1.15f; Energy-=2; sprintedTimer=0;
                rig.Shake(.8f,.2f); Footstep(1); game.Noise(transform.position,12);
            }
            float burden=0;
            if(game.Local.held>=0 && game.Local.held<game.Loot.Count)burden=game.Loot[game.Local.held].GripEffort(game.Local);
            float speed=Running ? Mathf.Lerp(WalkSpeed,SprintCap,sprintLerp) : Crouching ? CrouchSpeed : WalkSpeed;
            speed*=Mathf.Lerp(1,.62f,burden);
            Vector3 target=wish*speed;
            if(Sliding)
            {
                slideTimer-=dt; target=slideVelocity*Mathf.Clamp01(slideTimer/SlideTime)+wish*CrouchSpeed*.5f;
                if(slideTimer<=0 || !Crouching)Sliding=false;
            }
            planar=Vector3.Lerp(planar,target,1-Mathf.Exp(-(grounded ? (Sliding ? 3 : Friction) : AirFriction)*dt));

            if(grounded && vertical<0)vertical=-2;
            if(grounded)airJumps=game.Local.jumps;
            bool airJump=jumpBuffer>0 && groundBuffer<=0 && airJumps>0 && jumpCooldown<=0 && !Crawling;
            if((jumpBuffer>0 && groundBuffer>0 && jumpCooldown<=0 && !Crawling) || airJump)
            {
                // Extra jumps are an upgrade, as in R.E.P.O.: a weaker kick in mid air.
                if(airJump){airJumps--;vertical=JumpSpeed*.85f;rig.Shake(.6f,.15f);} else vertical=JumpSpeed;
                jumpBuffer=0; groundBuffer=0; jumpCooldown=.2f; rig.Jump(); Sliding=false;
            }
            else if(!grounded)vertical-=Gravity*dt;
            if(!grounded)fallSpeed=Mathf.Max(fallSpeed,-vertical);

            float height=Crouching ? CrouchHeight : StandHeight;
            controller.height=height; controller.center=Vector3.up*(height*.5f);
            EyeHeight=Mathf.Lerp(EyeHeight,Crouching ? CrouchEye : StandEye,1-Mathf.Exp(-12*dt));
            eyes.localPosition=Vector3.up*EyeHeight;

            CollisionFlags flags=controller.Move((planar+Vector3.up*vertical)*dt);
            if((flags & CollisionFlags.Above)!=0 && vertical>0)vertical=0;
            // Walls absorb momentum instead of storing it for when the student slips past.
            Vector3 actual=controller.velocity; actual.y=0;
            if((flags & CollisionFlags.Sides)!=0 && actual.magnitude<planar.magnitude)planar=actual;

            if(grounded && !wasGrounded && fallSpeed>4.5f)
            {
                float strength=Mathf.Clamp01((fallSpeed-4.5f)/8);
                rig.Land(strength); Footstep(1); game.Noise(transform.position,6+strength*16);
            }
            if(grounded)fallSpeed=0;
            wasGrounded=grounded;
            Speed=planar.magnitude;
            return grounded;
        }
        // Like R.E.P.O.'s aim targets: a caretaker charging into view yanks the camera towards it.
        private void ScanEnemies(float dt)
        {
            if(pullTimer>0)
            {
                pullTimer-=dt;
                Vector3 direction=pullPoint-View.transform.position;
                float targetYaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                float targetPitch=-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg;
                float k=1-Mathf.Exp(-9*dt);
                Yaw=Mathf.LerpAngle(Yaw,targetYaw,k); Pitch=Mathf.Lerp(Pitch,Mathf.Clamp(targetPitch,-70,80),k);
            }
            if(Time.time<scanAt || game.Phase != 1)return;
            scanAt=Time.time+.25f;
            Vector3 from=View.transform.position;
            foreach(RemakeEnemy enemy in game.Enemies)
            {
                Vector3 head=enemy.transform.position+Vector3.up*1.7f;
                float distance=Vector3.Distance(from,head);
                float before=enemyDistance.TryGetValue(enemy,out float d) ? d : distance;
                enemyDistance[enemy]=distance;
                if(distance>13 || (before-distance)/.25f<1.6f || Time.time<nextScare || !game.Local.alive)continue;
                if(Physics.Raycast(from,(head-from).normalized,distance-.4f,game.WorldMask,QueryTriggerInteraction.Ignore))continue;
                pullPoint=head; pullTimer=.55f; nextScare=Time.time+14;
                rig.Shake(2.2f,.45f); rig.Zoom(-14,.9f); footsteps.PlayOneShot(scare,.9f);
            }
        }
        private void Probe()
        {
            AimedLoot=null;AimedDoor=null;
            if(Physics.Raycast(View.transform.position,View.transform.forward,out RaycastHit hit,2.6f,~(1<<9),QueryTriggerInteraction.Ignore))
            {
                AimedLoot=hit.collider.GetComponentInParent<RemakeLoot>();
                AimedDoor=hit.collider.GetComponentInParent<HingedDoor>();
                aimedPoint=hit.point;
            }
        }
        private void LateUpdate()
        {
            StudentState state=LocalPlayer ? game.Local : remote;
            if(state == null)return;
            ApplySkin(state.skin);
            if(!LocalPlayer && animator!=null && animator.isHuman && state.alive && !state.tumble) {
                var neck=animator.GetBoneTransform(HumanBodyBones.Neck);
                var head=animator.GetBoneTransform(HumanBodyBones.Head);
                if(neck!=null)neck.rotation=Quaternion.AngleAxis(Mathf.Clamp(state.pitch,-35,40)*.25f,transform.right)*neck.rotation;
                if(head!=null)head.rotation=Quaternion.AngleAxis(Mathf.Clamp(state.pitch,-35,40),transform.right)*transform.rotation*neutralHeadRotation;
                if(Speed<.2f && state.eye>1.2f && state.held<0) {
                    RelaxArm(HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,-1);
                    RelaxArm(HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,1);
                }
            }
            bool holding=state.held>=0 && state.held<game.Loot.Count;
            Transform view=LocalPlayer ? View.transform : null;
            for(int side=0;side<2;side++)
            {
                bool visible=state.alive && (LocalPlayer || holding);
                for(int segment=0;segment<3;segment++)arms[side,segment].gameObject.SetActive(visible);
                if(!visible)continue;
                float sign=side==0?-1:1;
                Vector3 shoulder=LocalPlayer ? view.TransformPoint(sign*.23f,-.25f,.05f) : transform.TransformPoint(sign*.22f,1.25f,.07f);
                float sway=Mathf.Sin(Time.time*1.7f+sign*.4f)*.004f;
                Vector3 idle=LocalPlayer ? view.TransformPoint(sign*.20f,-.32f+sway,.34f) : transform.TransformPoint(sign*.25f,1,.4f);
                Quaternion rotation=LocalPlayer?view.rotation*Quaternion.Euler(-32,sign*18,-sign*18):transform.rotation;
                RemakeLoot item=holding?game.Loot[state.held]:null;
                Vector3 anchor=holding?item.transform.TransformPoint(state.grabLocal):idle;
                Vector3 right=Vector3.Cross(Vector3.up,state.aim).normalized;
                if(right.sqrMagnitude<.01f)right=transform.right;
                float effort=holding?item.GripEffort(state):0;
                Vector3 hand=hands[side].Pose(idle,rotation,item,anchor,right,state.aim,effort);
                Vector3 elbow=Vector3.Lerp(shoulder,hand,.5f)+Vector3.down*(.12f+effort*.07f)+right*sign*.035f;
                Limb(arms[side,0],shoulder,elbow,.036f);Limb(arms[side,1],elbow,hand,.025f);

            }
        }
        private static void Limb(Transform limb,Vector3 from,Vector3 to,float radius)
        {
            limb.position=(from+to)*.5f;limb.rotation=Quaternion.FromToRotation(Vector3.up,to-from);
            limb.localScale=new Vector3(radius*2,(to-from).magnitude*.5f,radius*2);
        }
        private void RelaxArm(HumanBodyBones upperName,HumanBodyBones lowerName,HumanBodyBones handName,float side) {
            var upper=animator.GetBoneTransform(upperName);var lower=animator.GetBoneTransform(lowerName);var hand=animator.GetBoneTransform(handName);
            if(upper==null||lower==null||hand==null)return;
            Vector3 direction=(Vector3.down+transform.right*side*.12f+transform.forward*.08f).normalized;
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation;
            direction=(Vector3.down+transform.forward*.16f+transform.right*side*.04f).normalized;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,direction)*lower.rotation;
        }
    }
}
