
/// This mod was made by xynzhere, aka xynz, or simply X (xynz_ on Discord).

// This is my first ever mod, so its not perfect, but it works pretty well.
// It might not be compatible with some other mods, but you can fix that by changing or adding things.
// I used AI for a few small parts because i couldnt figure out how to fix certain issues, and im still an amateur.
// Thank you for using my mod, or maybe youre just checking the source code to find something "interesting" arent you?

/// 
///               -         --
///              ------   ------
///             ------- -----------
///            ------- ------------
///              ------ --------------
///            -----------------------
///           ------------------------        -----------------------------
///          ---  -------------------          ----------------------------  
///          -----------------------      ----------------
///            ---------------------    ---------------
///          -----------------------  ---------------
///         -----------------------  ---------------
///        -------------------------------------
///       --------------------------------------
///      --------------------------------------
///      -------------------------------------
///     ------------------------------------
///     -----------------------------------
///      --------------------------------
///       ------------------------------
///       -----------------------------
///      -- --   -----------------------
///      - --     -----------------------
///     -  --       ---------------------
///    -----      ----------------------
///      --      ----------------------
///     --      --------------------
///     --     --------------------
///    --    --------------------
///   ---   -----------------
///   --  ---------------
///  --- ------ -----
///  --  ------ ---
///      ----  --
///     ---
///     --
///     

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using ghop.Classes;
using GorillaLocomotion;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using Valve.VR;

namespace GHop.Classes
{
    [BepInPlugin("com.xynz.ghop", "GHop", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;
        public static ManualLogSource log;
        public static bool bhopEnabled = false;
        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip>();

        public bool isActive;

        private Harmony harmony;
        private StrafeMovement strafe;
        private Rigidbody playerRb;

        private float sensitivity = 0.333f;
        private float bhopKeyDelay;
        private bool cursorLocked = true;
        private bool IsSteam = false;

        private bool lastBKey;
        private bool lastVKey;
        private bool lastCKey;
        private bool lastNKey;

        private bool thirdPersonActive = false;
        private bool freecamActive = false;
        private float freecamSpeed = 8f;
        private float freecamSprintMultiplier = 4f;

        private GorillaCameraFollow camFollow;
        private GameObject thirdPersonCamObj;
        private Camera mainCam;

        private Texture2D crosshairTex;

        private Canvas logoCanvas;
        private Image logoImage;

        private Canvas fadeCanvas;
        private Image fadeImage;
        private bool fading = false;
        private float fadeTimer = 0f;
        private float fadeHoldDuration = 1f;
        private float fadeOutDuration = 3f;

        private static GameObject audiomgr;

        private List<AudioClip> footstepClips = new List<AudioClip>();
        private Vector3 lastFootstepPosition;
        private float minFootstepVolume = 0.24f;
        private float maxFootstepVolume = 0.28f;
        private float stepThreshold = 0.9f;
        private float lastFootstepTime = 0f;
        private float footstepCooldown = 0.18f;

        private List<AudioClip> fallClips = new List<AudioClip>();
        private float fallAirTime = 0f;
        private float fallStartY = 0f;
        private bool isFalling = false;
        private float fallThreshold = 4f;

        private float currentTilt = 0f;
        private float tiltVelocity = 0f;
        private float targetTilt = 0f;

        private float walkAnimTime = 0f;
        private float walkAnimSpeed = 0f;
        private float walkAnimSpeedTarget = 0f;

        private float bodyYaw = 0f;
        private float headBodyAngle = 0f;
        private float randomOffsetLeft = 0f;
        private float randomOffsetRight = 0f;
        private float randomOffsetTimer = 0f;

        private float smoothRandomLeft = 0f;
        private float smoothRandomRight = 0f;

        private float bodyTiltZ = 0f;
        private float bodyTiltZTarget = 0f;
        private float bodyTiltZTimer = 0f;
        private float bodyTiltZVelocity = 0f;

        private GameObject rig;
        private GameObject gorilla;

        private void TriggerLandingTilt(float fallDistance)
        {
            float tiltStrength = Mathf.Clamp(fallDistance * 2.5f, 4f, 30f);
            float tiltDir = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            targetTilt = tiltStrength * tiltDir;
            currentTilt = targetTilt;
            tiltVelocity = 0f;
        }

        private void UpdateTiltSpring()
        {
            if (Mathf.Abs(currentTilt) < 0.01f && Mathf.Abs(tiltVelocity) < 0.01f)
            {
                currentTilt = 0f;
                tiltVelocity = 0f;
                return;
            }

            float stiffness = 47f;
            float damping = 7f;
            float springForce = -stiffness * currentTilt;
            float dampForce = -damping * tiltVelocity;
            tiltVelocity += (springForce + dampForce) * Time.deltaTime;
            currentTilt += tiltVelocity * Time.deltaTime;
        }

        private void Start()
        {
            instance = this;
            log = Logger;

            harmony = new Harmony("com.xynz.ghop");
            harmony.PatchAll();

            playerRb = GTPlayer.Instance.GetComponent<Rigidbody>();

            CreateFadeScreen();
            StartCoroutine(PlayIntroNextFrame());
            StartCoroutine(PlayLogoIntro());

            rig = GameObject.Find("Player Objects/Local VRRig/Local Gorilla Player/rig");
            gorilla = GameObject.Find("Player Objects/Local VRRig/Local Gorilla Player/gorilla_new");
            breathingCoroutine = StartCoroutine(BreathingAnimation());

            mainCam = Camera.main;

            var followGO = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/Main Camera/Camera Follower");
            if (followGO != null)
                camFollow = followGO.GetComponent<GorillaCameraFollow>();

            thirdPersonCamObj = GameObject.Find("Player Objects/Third Person Camera");

            lastFootstepPosition = GTPlayer.Instance.transform.position;

            for (int i = 1; i <= 2; i++)
            {
                AudioClip clip = LoadSoundFromResource($"Sounds.fall{i}.wav");
                if (clip != null) fallClips.Add(clip);
            }

            for (int i = 1; i <= 10; i++)
            {
                AudioClip clip = LoadSoundFromResource($"Sounds.fs{i}.wav");
                if (clip != null) footstepClips.Add(clip);
            }

            if (thirdPersonCamObj != null) thirdPersonCamObj.SetActive(false);
            thirdPersonActive = false;

            isActive = true;
            bhopEnabled = true;
            strafe = GTPlayer.Instance.gameObject.AddComponent<StrafeMovement>();
            strafe.camObj = GTPlayer.Instance.bodyCollider.gameObject;
        }

        private void Update()
        {
            HandleFade();
            HandleFreecam();
            HandleThirdPerson();
            HandleBhop();
            HandleCursorLock();
            HandleMouseLook();
            UpdateTiltSpring();
            HandleFootsteps();
            HandleFallSound();
            HandleIdlePose();

            bool isThirdPerson = GorillaTagger.Instance.thirdPersonCamera.gameObject.activeSelf;
            if (rig != null) rig.SetActive(isThirdPerson);
            if (gorilla != null) gorilla.SetActive(isThirdPerson);
        }

        private void HandleFade()
        {
            if (!fading) return;

            fadeTimer += Time.deltaTime;

            if (fadeTimer < fadeHoldDuration)
            {
                fadeImage.color = new Color(0f, 0f, 0f, 1f);
            }
            else
            {
                float elapsed = fadeTimer - fadeHoldDuration;
                float alpha = Mathf.Clamp01(1f - (elapsed / fadeOutDuration));
                fadeImage.color = new Color(0f, 0f, 0f, alpha);

                if (alpha <= 0f)
                {
                    fading = false;
                    fadeImage.gameObject.SetActive(false);
                }
            }
        }

        private void HandleFreecam()
        {
            bool nHeld = UnityInput.Current.GetKey(KeyCode.N);

            if (nHeld && !lastNKey)
            {
                freecamActive = !freecamActive;

                if (freecamActive)
                {
                    GTPlayer.Instance.disableMovement = true;
                    VRRig.LocalRig.head.rigTarget.transform.parent = null;
                }
                else
                {
                    GTPlayer.Instance.disableMovement = false;
                }
            }
            lastNKey = nHeld;

            if (freecamActive)
            {
                Vector3 move = Vector3.zero;
                float speed = freecamSpeed;
                if (UnityInput.Current.GetKey(KeyCode.LeftShift)) speed *= freecamSprintMultiplier;
                if (UnityInput.Current.GetKey(KeyCode.W)) move += mainCam.transform.forward;
                if (UnityInput.Current.GetKey(KeyCode.S)) move -= mainCam.transform.forward;
                if (UnityInput.Current.GetKey(KeyCode.A)) move -= mainCam.transform.right;
                if (UnityInput.Current.GetKey(KeyCode.D)) move += mainCam.transform.right;
                if (UnityInput.Current.GetKey(KeyCode.Space)) move += Vector3.up;
                if (UnityInput.Current.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

                if (playerRb != null)
                {
                    playerRb.useGravity = false;
                    playerRb.linearVelocity = Vector3.zero;
                    playerRb.angularVelocity = Vector3.zero;
                    playerRb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                }
                GTPlayer.Instance.transform.position += move * speed * Time.deltaTime;
            }
            else
            {
                if (playerRb != null)
                {
                    playerRb.useGravity = true;
                    playerRb.constraints = RigidbodyConstraints.FreezeRotation;
                }
            }
        }

        private void HandleThirdPerson()
        {
            bool cHeld = UnityInput.Current.GetKey(KeyCode.C);

            if (cHeld && !lastCKey)
            {
                thirdPersonActive = !thirdPersonActive;

                if (thirdPersonActive)
                {
                    ApplyThirdPersonSettings();
                    if (thirdPersonCamObj != null) thirdPersonCamObj.SetActive(true);
                }
                else
                {
                    if (thirdPersonCamObj != null) thirdPersonCamObj.SetActive(false);
                }
            }
            lastCKey = cHeld;
        }

        private void HandleBhop()
        {
            bool bHeld = UnityInput.Current.GetKey(KeyCode.B);

            if (bHeld && !lastBKey && Time.time > bhopKeyDelay)
            {
                if (!isActive)
                {
                    isActive = true;
                    bhopEnabled = true;
                    strafe = GTPlayer.Instance.gameObject.AddComponent<StrafeMovement>();
                    strafe.camObj = GTPlayer.Instance.bodyCollider.gameObject;
                }
                else
                {
                    isActive = false;
                    bhopEnabled = false;
                    bhopKeyDelay = Time.time + 0.3f;
                    Destroy(strafe);
                    GTPlayer.Instance.disableMovement = false;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            lastBKey = bHeld;

        }

        private void HandleCursorLock()
        {
            bool vHeld = UnityInput.Current.GetKey(KeyCode.V);
            if (vHeld && !lastVKey) cursorLocked = !cursorLocked;
            lastVKey = vHeld;

            if (cursorLocked && !XRSettings.isDeviceActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void HandleMouseLook()
        {
            if (XRSettings.isDeviceActive || strafe == null) return;

            Transform camParent = GTPlayer.Instance.GetControllerTransform(false).parent;
            Vector3 euler = camParent.rotation.eulerAngles;

            float newX = euler.x - Mouse.current.delta.value.y * sensitivity;
            float newY = euler.y + Mouse.current.delta.value.x * sensitivity;

            newX = newX > 180f ? newX - 360f : newX;
            newX = Mathf.Clamp(newX, -90f, 90f);

            camParent.rotation = Quaternion.Euler(newX, newY, currentTilt);
            VRRig.LocalRig.head.rigTarget.transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation;
        }

        private void HandleFootsteps()
        {
            if (!isActive || strafe == null) return;
            if (playerRb == null || !IsGrounded()) return;

            Vector3 velocity = playerRb.linearVelocity;
            if (new Vector3(velocity.x, 0f, velocity.z).magnitude < 0.5f) return;
            if (Time.time - lastFootstepTime < footstepCooldown) return;

            Vector3 currentPos = GTPlayer.Instance.transform.position;
            Vector3 horizontalMove = new Vector3(
                currentPos.x - lastFootstepPosition.x,
                0f,
                currentPos.z - lastFootstepPosition.z
            );

            if (horizontalMove.magnitude >= stepThreshold)
            {
                lastFootstepPosition = currentPos;
                lastFootstepTime = Time.time;

                if (footstepClips.Count > 0)
                {
                    AudioClip clip = footstepClips[UnityEngine.Random.Range(0, footstepClips.Count)];
                    float volume = UnityEngine.Random.Range(minFootstepVolume, maxFootstepVolume);
                    AudioSource.PlayClipAtPoint(clip, currentPos, volume);
                }
            }
        }

        private void HandleFallSound()
        {
            if (!isActive) return;
            if (playerRb == null) return;

            float velY = playerRb.linearVelocity.y;
            if (velY < -2f && !isFalling)
            {
                isFalling = true;
                fallStartY = GTPlayer.Instance.transform.position.y;
                fallAirTime = 0f;
            }
            if (isFalling) fallAirTime += Time.deltaTime;
            if (isFalling && fallAirTime > 0.3f && IsGrounded() && Mathf.Abs(velY) < 1f)
            {
                float fallDistance = fallStartY - GTPlayer.Instance.transform.position.y;
                lastFootstepTime = Time.time + footstepCooldown;
                if (fallDistance >= fallThreshold)
                {
                    if (fallClips.Count > 0)
                        Play2DAudio(fallClips[UnityEngine.Random.Range(0, fallClips.Count)], 0.33f);
                    TriggerLandingTilt(fallDistance);
                }
                else
                {
                    PlayLand();
                }
                isFalling = false;
                fallStartY = 0f;
            }
        }

        private void OnGUI()
        {
            if (!isActive) return;

            if (crosshairTex == null)
                crosshairTex = CreateCircleTexture(32, 10f, 2f);

            float size = 6f;
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;
            GUI.DrawTexture(new Rect(cx - size / 2f, cy - size / 2f, size, size), crosshairTex);
        }

        private bool IsGrounded()
        {
            Vector3 origin = GTPlayer.Instance.bodyCollider.transform.position;
            Bounds bounds = GTPlayer.Instance.bodyCollider.bounds;
            return Physics.SphereCast(
                origin,
                bounds.extents.x * 0.8f,
                Vector3.down,
                out _,
                bounds.extents.y + 0.15f,
                (int)GTPlayer.Instance.locomotionEnabledLayers
            );
        }

        private void ApplyThirdPersonSettings()
        {
            if (camFollow == null) return;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = camFollow.GetType();

            type.GetField("baseFollowDistance", flags)?.SetValue(camFollow, 2.6f);
            type.GetField("baseShoulderOffset", flags)?.SetValue(camFollow, Vector3.zero);
            type.GetField("baseVerticalArmLength", flags)?.SetValue(camFollow, 0.4f);

            var cinemachineField = type.GetField("cinemachineFollow", flags);
            if (cinemachineField != null)
            {
                var cinemachineFollow = cinemachineField.GetValue(camFollow);
                if (cinemachineFollow != null)
                {
                    var dampingField = cinemachineFollow.GetType().GetField("Damping", flags);
                    dampingField?.SetValue(cinemachineFollow, Vector3.zero);
                }
            }
        }

        private IEnumerator LandingTilt()
        {
            float fallDistance = fallStartY - GTPlayer.Instance.transform.position.y;
            float tiltStrength = Mathf.Clamp(fallDistance * 2.5f, 4f, 30f);
            float tiltDir = UnityEngine.Random.value > 0.5f ? 1f : -1f;
            targetTilt = tiltStrength * tiltDir;
            currentTilt = targetTilt;
            tiltVelocity = 0f;
            yield return null;
        }

        private IEnumerator PlayIntroNextFrame()
        {
            yield return null;
            Play2DAudio(LoadSoundFromResource("Sounds.intro.wav"), 0.3f);
        }

        private IEnumerator PlayLogoIntro()
        {
            yield return new WaitForSeconds(0.5f);

            GameObject canvasObj = new GameObject("LogoCanvas");
            logoCanvas = canvasObj.AddComponent<Canvas>();
            logoCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);

            GameObject imgObj = new GameObject("LogoImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            logoImage = imgObj.AddComponent<Image>();

            Texture2D tex = LoadTextureFromResources("logo");
            if (tex == null) yield break;

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            logoImage.sprite = sprite;

            RectTransform rect = logoImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(20f, 20f);
            rect.sizeDelta = new Vector2(200f, 200f);

            for (int i = 0; i < 5; i++)
            {
                yield return FadeImage(logoImage, 0f, 1f, 0.5f);
                yield return FadeImage(logoImage, 1f, 0f, 0.5f);
            }

            Destroy(canvasObj);
        }

        private IEnumerator FadeImage(Image img, float from, float to, float duration)
        {
            float t = 0f;
            Color c = img.color;

            while (t < duration)
            {
                t += Time.deltaTime;
                img.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }

            img.color = new Color(c.r, c.g, c.b, to);
        }

        private void CreateFadeScreen()
        {
            GameObject canvasObj = new GameObject("FadeCanvas");
            fadeCanvas = canvasObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasObj);

            GameObject imgObj = new GameObject("FadeImage");
            imgObj.transform.SetParent(canvasObj.transform, false);
            fadeImage = imgObj.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 1f);

            RectTransform rect = imgObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            fading = true;
            fadeTimer = 0f;

            fadeCanvas.sortingOrder = 10;
        }

        private float handYOffset = 0.79f;
        private Coroutine breathingCoroutine;
        private float breatheTime = 0f;
        private float breatheOffset = 0f;
        private float landingSquash = 0f;
        private float landingSquashVelocity = 0f;

        private IEnumerator BreathingAnimation()
        {
            while (true)
            {
                breatheTime += Time.deltaTime;

                float primary = Mathf.Sin(breatheTime * 0.8f);
                float secondary = Mathf.Sin(breatheTime * 2.1f) * 0.3f;
                float combined = primary + secondary;
                float eased = Mathf.Sign(combined) * Mathf.Pow(Mathf.Abs(combined), 0.5f);

                breatheOffset = eased * 0.007f;

                yield return null;
            }
        }

        private void HandleIdlePose()
        {
            Vector3 flatVel = new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            float speed = flatVel.magnitude;
            bool grounded = IsGrounded();

            float headYaw = GorillaTagger.Instance.headCollider.transform.eulerAngles.y;
            headBodyAngle = Mathf.DeltaAngle(bodyYaw, headYaw);

            float maxAngle = 35f;
            if (Mathf.Abs(headBodyAngle) > maxAngle)
            {
                float excess = headBodyAngle - Mathf.Sign(headBodyAngle) * maxAngle;
                bodyYaw = Mathf.LerpAngle(bodyYaw, bodyYaw + excess, Time.deltaTime * 10f);
            }

            if (speed > 0.1f)
                bodyYaw = Mathf.LerpAngle(bodyYaw, headYaw, Time.deltaTime * 8f);

            randomOffsetTimer -= Time.deltaTime;
            if (randomOffsetTimer <= 0f)
            {
                randomOffsetLeft = UnityEngine.Random.Range(-0.004f, 0.004f);
                randomOffsetRight = UnityEngine.Random.Range(-0.004f, 0.004f);
                randomOffsetTimer = UnityEngine.Random.Range(1.5f, 3f);
            }
            smoothRandomLeft = Mathf.Lerp(smoothRandomLeft, randomOffsetLeft, Time.deltaTime * 1.5f);
            smoothRandomRight = Mathf.Lerp(smoothRandomRight, randomOffsetRight, Time.deltaTime * 1.5f);

            float noiseLeft = Mathf.Sin(Time.time * 0.7f) * 0.002f + smoothRandomLeft;
            float noiseRight = Mathf.Sin(Time.time * 0.6f + 1f) * 0.002f + smoothRandomRight;

            if (grounded && playerRb.linearVelocity.y < -1f)
            {
                float impact = Mathf.Clamp01(Mathf.Abs(playerRb.linearVelocity.y) / 8f);
                if (landingSquashVelocity > -0.01f)
                    landingSquashVelocity = -impact * 0.12f;
            }
            landingSquashVelocity += (-18f * landingSquash - 8f * landingSquashVelocity) * Time.deltaTime;
            landingSquash += landingSquashVelocity * Time.deltaTime;

            if (!grounded)
                walkAnimSpeedTarget = 0f;
            else if (speed > 0.1f)
                walkAnimSpeedTarget = Mathf.Clamp(speed * 3f, 1.5f, 14f);
            else
                walkAnimSpeedTarget = 0f;

            walkAnimSpeed = Mathf.Lerp(walkAnimSpeed, walkAnimSpeedTarget, Time.deltaTime * 10f);
            walkAnimTime += Time.deltaTime * walkAnimSpeed;

            float walkBlend = Mathf.Clamp01(walkAnimSpeed / 1.5f);
            float idleBlend = 1f - walkBlend;

            bodyTiltZTimer -= Time.deltaTime;
            if (bodyTiltZTimer <= 0f)
            {
                float tiltRange = Mathf.Lerp(1.5f, 4f, walkBlend);
                bodyTiltZTarget = UnityEngine.Random.Range(-tiltRange, tiltRange);
                bodyTiltZTimer = UnityEngine.Random.Range(1.5f, 3.5f);
            }
            bodyTiltZVelocity += (-3f * (bodyTiltZ - bodyTiltZTarget) - 4f * bodyTiltZVelocity) * Time.deltaTime;
            bodyTiltZ += bodyTiltZVelocity * Time.deltaTime;

            float walkRoll = Mathf.Sin(walkAnimTime) * 5f * walkBlend * Mathf.Clamp01(walkAnimSpeed / 3f);
            float swingAmount = Mathf.Clamp(walkAnimSpeed * 0.015f, 0f, 0.18f);

            Vector3 localVel = Quaternion.Inverse(Quaternion.Euler(0f, bodyYaw, 0f)) * new Vector3(playerRb.linearVelocity.x, 0f, playerRb.linearVelocity.z);
            float forwardSpeed = localVel.z / Mathf.Max(flatVel.magnitude, 0.001f);

            float forwardSwing = swingAmount * forwardSpeed;
            float leftSwing = Mathf.Sin(walkAnimTime) * forwardSwing;
            float rightSwing = Mathf.Sin(walkAnimTime + Mathf.PI) * forwardSwing;

            float bobAmount = swingAmount * 0.3f;
            float leftBob = Mathf.Abs(Mathf.Sin(walkAnimTime)) * bobAmount;
            float rightBob = Mathf.Abs(Mathf.Sin(walkAnimTime + Mathf.PI)) * bobAmount;

            float swayAmount = swingAmount * 0.2f;
            float sway = Mathf.Sin(walkAnimTime * 0.5f) * swayAmount;

            float headBob = Mathf.Abs(Mathf.Sin(walkAnimTime)) * 0.008f * walkBlend;
            float headTiltX = Mathf.Sin(walkAnimTime) * 1.5f * walkBlend;
            float headSway = Mathf.Sin(walkAnimTime * 0.5f) * 1f * walkBlend;

            float leftY = (handYOffset - leftBob + noiseLeft) * idleBlend + (0.77f - leftBob + noiseLeft) * walkBlend;
            float rightY = (handYOffset - rightBob + noiseRight) * idleBlend + (0.77f - rightBob + noiseRight) * walkBlend;

            leftY += landingSquash;
            rightY += landingSquash;

            GorillaTagger.Instance.bodyCollider.transform.localScale = new Vector3(1f, 2f, 1f);
            GorillaTagger.Instance.bodyCollider.transform.localPosition = new Vector3(0f, 0f, 0f);
            VRRig.LocalRig.enabled = false;

            VRRig.LocalRig.transform.position = GorillaTagger.Instance.bodyCollider.transform.position
                + new Vector3(0f, breatheOffset + headBob - landingSquash, 0f);

            Quaternion yawRot = Quaternion.Euler(0f, bodyYaw, 0f);
            Quaternion rollRot = Quaternion.AngleAxis(bodyTiltZ + walkRoll, yawRot * Vector3.up);
            VRRig.LocalRig.transform.rotation = rollRot * yawRot;

            VRRig.LocalRig.head.rigTarget.transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation
                * Quaternion.Euler(headTiltX, headSway, 0f);

            Vector3 leftHandBase = VRRig.LocalRig.transform.position + VRRig.LocalRig.transform.right * (-0.23f + sway * walkBlend);
            Vector3 rightHandBase = VRRig.LocalRig.transform.position + VRRig.LocalRig.transform.right * (0.23f + sway * walkBlend);

            VRRig.LocalRig.leftHand.rigTarget.transform.position = leftHandBase + VRRig.LocalRig.transform.forward * leftSwing - new Vector3(0f, leftY + breatheOffset, 0f);
            VRRig.LocalRig.rightHand.rigTarget.transform.position = rightHandBase + VRRig.LocalRig.transform.forward * rightSwing - new Vector3(0f, rightY + breatheOffset, 0f);

            float leftRotX = swingAmount > 0 ? leftSwing / swingAmount * 12f * walkBlend : 0f;
            float rightRotX = swingAmount > 0 ? rightSwing / swingAmount * 12f * walkBlend : 0f;
            float swayRoll = sway / (swayAmount + 0.0001f) * 3f * walkBlend;

            VRRig.LocalRig.leftHand.rigTarget.transform.rotation = VRRig.LocalRig.transform.rotation * Quaternion.Euler(190f + leftRotX, -90f, -90f + swayRoll);
            VRRig.LocalRig.rightHand.rigTarget.transform.rotation = VRRig.LocalRig.transform.rotation * Quaternion.Euler(190f + rightRotX, 90f, 90f + swayRoll);
        }
        private Texture2D CreateCircleTexture(int size, float radius, float outlineThickness)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            Color clear = new Color(0, 0, 0, 0);
            Color fill = new Color(1f, 1f, 1f, 0.5f);
            Color outline = new Color(0f, 0f, 0f, 0.5f);
            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius) tex.SetPixel(x, y, fill);
                    else if (dist <= radius + outlineThickness + 1f) tex.SetPixel(x, y, outline);
                    else tex.SetPixel(x, y, clear);
                }
            }
            tex.Apply();
            return tex;
        }

        private Texture2D LoadTextureFromResources(string name)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string resourceName = null;

                foreach (string res in asm.GetManifestResourceNames())
                {
                    if (res.EndsWith(name + ".png"))
                    {
                        resourceName = res;
                        break;
                    }
                }

                if (resourceName == null) return null;

                using (Stream stream = asm.GetManifestResourceStream(resourceName))
                {
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(data);
                    return tex;
                }
            }
            catch { return null; }
        }

        public static void PlayLand()
        {
            Play2DAudio(LoadSoundFromResource("Sounds.land.wav"), 0.3f);
        }

        public Vector2 GetLeftJoystickAxis()
        {
            if (IsSteam)
                return SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.GetAxis(SteamVR_Input_Sources.LeftHand);

            Vector2 result = Vector2.zero;
            ((UnityEngine.XR.InputDevice)ControllerInputPoller.instance.leftControllerDevice)
                .TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out result);
            return result;
        }

        public static AudioClip LoadWav(byte[] wavFile)
        {
            int channels = BitConverter.ToInt16(wavFile, 22);
            int sampleRate = BitConverter.ToInt32(wavFile, 24);

            int i = 12;
            int dataSize = 0;
            int chunkSize;

            for (; i < wavFile.Length; i += 8 + chunkSize)
            {
                string chunkId = Encoding.ASCII.GetString(wavFile, i, 4);
                chunkSize = BitConverter.ToInt32(wavFile, i + 4);
                if (chunkId == "data") { dataSize = chunkSize; i += 8; break; }
            }

            int sampleCount = dataSize / 2;
            float[] samples = new float[sampleCount];

            for (int j = 0; j < sampleCount; j++)
                samples[j] = BitConverter.ToInt16(wavFile, i + j * 2) / 32768f;

            AudioClip clip = AudioClip.Create("EmbeddedClip", sampleCount / channels, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public static AudioClip LoadSoundFromResource(string endsWith)
        {
            if (audioPool.ContainsKey(endsWith)) return audioPool[endsWith];

            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = null;

            foreach (string res in asm.GetManifestResourceNames())
            {
                if (res.EndsWith(endsWith))
                {
                    resourceName = res;
                    break;
                }
            }

            if (resourceName == null) { Debug.LogError($"failed to load resource: {endsWith}"); return null; }

            using (Stream stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null) { Debug.LogError($"failed to open stream: {resourceName}"); return null; }

                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                AudioClip clip = LoadWav(data);
                audioPool.Add(endsWith, clip);
                return clip;
            }
        }

        public static void Play2DAudio(AudioClip sound, float volume)
        {
            if (sound == null) return;

            if (audiomgr == null)
            {
                audiomgr = new GameObject("2daudiomgr");
                DontDestroyOnLoad(audiomgr);
                audiomgr.AddComponent<AudioSource>().spatialBlend = 0f;
            }

            AudioSource src = audiomgr.GetComponent<AudioSource>();
            src.volume = volume;
            src.clip = sound;
            src.Play();
        }
    }
}

// Hope your day/night is going well, nice seeing you down here reading this :D