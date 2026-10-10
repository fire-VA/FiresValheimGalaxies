using UnityEngine;

namespace VerdantsAscent.Water.Core
{
    /// <summary>
    /// Spawns a real 3D moon (FBX + albedo bundled in `fireswater` bundle) at
    /// the antipode of Valheim's sun direction (with an orbital offset to
    /// drive natural phases) and parents it to the main camera so it tracks
    /// the player's view without sliding.
    ///
    /// === WHY THIS EXISTS ===
    /// Vanilla "moon" is a circular HOLE in the skybox with a stretched
    /// 2D texture behind it — it's not a real 3D moon, has no phases,
    /// and the texture deforms badly when _MoonSize is anything but ~0.1.
    /// Reference: Balrond Discord investigation 2026-05-21.
    /// Since we already control the skybox shader, we suppress the vanilla
    /// hole (_HideVanillaMoon=1) and render our own sphere on top.
    ///
    /// === PHASE PHYSICS ===
    /// The moon is rendered with FiresMoon shader which does Lambertian
    /// diffuse from _SkyboxSunDir (the SUN direction). So the lit hemisphere
    /// of the sphere always faces the sun. Phases come naturally from where
    /// the moon sits in its orbit relative to the sun-camera line.
    ///
    /// PURE ANTIPODE = full moon always (vanilla Valheim behavior). To get
    /// real phases we offset the moon along an orbital plane by an angle
    /// proportional to (gameDay mod SynodicDays) / SynodicDays. As the offset
    /// progresses the moon walks New → Crescent → Quarter → Gibbous → Full →
    /// Waning Gibbous → Last Quarter → Waning Crescent → New, repeating
    /// every SynodicDays in-game days.
    ///
    /// === SYNC ACROSS CLIENTS ===
    /// Phase is derived purely from EnvMan.GetDay() (game day count) which
    /// is server-authoritative. All clients see the same phase at the same
    /// game time. No RPC needed.
    /// </summary>
    public static class FiresMoon
    {
        // 2026-06-01: switched from the bare FBX asset to the VAFIREMOON
        // piece prefab the user authored — it bundles MeshFilter+Renderer+
        // LODGroup wired up correctly under a known hierarchy. We instantiate
        // the prefab and strip the Piece chassis (Piece, ZNetView, WearNTear,
        // BoxCollider, MeshCollider, snap points) before using it as a
        // sky-only renderer. If you later want to PLACE moons in the world
        // via Hammer, that goes through the FAP piece-registration path
        // separately — this file is sky-render only.
        public const string PrefabName   = "FiresMoon";           // FBX root in the firesgalaxies bundle
        public const string MaterialName = "FiresMoon_Material"; // built at runtime if absent
        public const string ShaderName   = "Custom/FiresMoon";
        public const string AlbedoName   = "FiresMoon_Albedo";

        // ===== "Go to moon" mode =====
        // When VisitMode is on, the moon stops camera-tracking and stays at
        // the world position recorded when VisitMode was turned on. Lets the
        // player physically walk/fly to and around the moon. va_goto_moon
        // toggles this on and teleports the player; va_unfreeze_moon toggles
        // it off and the moon returns to following the sky.
        public static bool   VisitMode    = false;
        public static Vector3 VisitWorldPos = Vector3.zero;
        public static float  VisitMoonRadius = 250f;  // physical radius the moon GameObject ends up at

        // ===== Configurable from C# (driven by ConfigManager via static fields) =====
        public static bool  Enabled       = true;
        // When true, the reachable orbital moon (FiresOrbitals, wired to the FiresMoon prefab)
        // is the visible moon, so hide THIS skybox 3D disc to avoid two moons — but keep
        // suppressing Valheim's flat vanilla moon so exactly one moon shows. Tides/phase logic
        // are unaffected (those live in TideSystem, not here).
        public static bool  SuppressDisc  = false;
        // World-space distance from camera. Safe to put very far because we
        // assign the moon to Unity layer 19 ("skybox") which is rendered by
        // Valheim's dedicated SkyboxCamera (depth=-2, far clip=100000m).
        // The main player camera does NOT render layer 19, so the moon
        // is rendered ONLY by the skybox camera and never frustum-culled.
        // 4500m was the original — keep it for the "infinite sky" feel.
        public static float Distance      = 4500f;

        // Unity layer index 19 — Valheim names this "skybox" in TagManager.
        // Vanilla SkyboxCamera renders ONLY this layer (m_CullingMask=524288=1<<19)
        // with a 100km far clip plane AND a 100m NEAR clip plane. Putting
        // our moon here is the proper place for SKY rendering.
        // Confirmed by reading TagManager.asset + SkyboxCamera.prefab.
        public const int   SkyboxLayer    = 19;

        // Layer to switch the moon onto in VisitMode so the MAIN camera
        // renders it. Main camera near clip is ~0.3m so the moon stays
        // visible right up close. Default layer (0) is rendered by every
        // standard camera.
        public const int   VisitLayer     = 0;
        public static float SizeScale     = 1.0f;    // multiplier on baseline visual diameter
        public static float SynodicDays   = 30f;     // game days per full lunar cycle
        public static float OrbitInclinationDeg = 8f;  // tilt of orbital plane vs ecliptic
        // The moon rides FiresOrbitals' tilted egg orbit, but its ANGLE is driven
        // by EnvMan's day fraction (one orbit per day/night cycle) — so it's high
        // at night (fraction 0 = midnight = apex), low by day (0.5 = noon = nadir),
        // and ANY mod that changes day length carries the orbit along automatically,
        // because GetDayFraction() is already normalised to the current day length.
        // We use only the DIRECTION (normalised); the sizes set the shape.
        private static OrbitParams MoonOrbit() => new OrbitParams
        {
            OrbitWidth        = 2f,
            ApexHeight        = 0.9f,
            NadirHeight       = -0.6f,
            PeriodSeconds     = 1f,    // angle = dayFraction * 2π (caller passes the day fraction as t)
            EggRatio          = 0.85f,
            WobbleSeconds     = 1f,
            PhaseOffsetDeg    = PhaseOffsetDays * 12f,
        };
        public static float PhaseOffsetDays = 0f;    // shift the starting phase (debug)
        public static float SunBrightness = 1.4f;    // lit-side brightness
        public static float DarkSide      = 0.04f;   // earthshine on dark side
        // Self-glow, now GATED BY THE LIT TERM in the shader (only the sunlit
        // face glows), so it no longer washes the new-moon side. Lowered from
        // 0.6: the lit hemisphere is already bright via SunBrightness, this is
        // just a gentle night-visibility boost on top — kept low to avoid bloom.
        public static float Emission      = 0.2f;
        public static Color EmissionTint  = Color.white;  // HDR tint multiplied into emission
        public static float TerminatorSoft = 0.18f;  // softness of the lit/dark boundary
        public static float Saturation    = 0.7f;    // 0=B&W, 1=full color

        // Blood moon — recolor the moon to deep red (lunar-eclipse look) while
        // keeping crater detail. 0 = normal, 1 = full blood. Set via va_moon_blood.
        public static float BloodMoon  = 0f;
        public static Color BloodColor = new Color(1.7f, 0.4f, 0.2f, 1f);
        // Manual phase override for previewing. <0 = auto (game-day driven).
        // 0..1 forces a synodic phase, driving BOTH the moon's position and its
        // lit fraction so you see the real look. Set via va_moon_phase.
        public static float PhaseOverride = -1f;

        // ===== Runtime state =====
        public  static Shader     MoonShader   { get; private set; }
        public  static Texture2D  MoonTexture  { get; private set; }
        public  static GameObject MoonPrefab   { get; private set; }
        public  static GameObject MoonInstance { get; private set; }
        public  static Material   MoonMaterial { get; private set; }

        private static readonly int s_propMasterFxOff   = Shader.PropertyToID("_FiresMasterFxOff");
        private static readonly int Prop_MainTex        = Shader.PropertyToID("_MainTex");
        private static readonly int Prop_SunBrightness  = Shader.PropertyToID("_SunBrightness");
        private static readonly int Prop_DarkSide       = Shader.PropertyToID("_DarkSide");
        private static readonly int Prop_Emission       = Shader.PropertyToID("_Emission");
        private static readonly int Prop_EmissionTint   = Shader.PropertyToID("_EmissionTint");
        private static readonly int Prop_TerminatorSoft = Shader.PropertyToID("_TerminatorSoft");
        private static readonly int Prop_Saturation     = Shader.PropertyToID("_Saturation");
        private static readonly int Prop_HideVanilla    = Shader.PropertyToID("_HideVanillaMoon");
        private static readonly int Prop_BloodMoon      = Shader.PropertyToID("_BloodMoon");
        private static readonly int Prop_BloodColor     = Shader.PropertyToID("_BloodColor");

        // Target apparent angular diameter of the moon as seen from camera,
        // in degrees. Real moon is 0.5°, vanilla Valheim moon disk reads as
        // ~3-4° (oversized for visibility but readable). 2.5° lands between
        // "real" and "vanilla" and stops the previous 6° default from filling
        // a quarter of the night sky. SizeScale multiplies this.
        public static float TargetAngularDiameterDeg = 2.5f;

        // Diagnostic throttle — print _SkyboxSunDir once per second so we
        // can see if EnvMan is actually setting it.
        private static float s_nextDiagAt;
        // Captured at instantiation from renderer bounds — the native
        // world-space radius of the FBX mesh at unit scale. UpdateMoonTransform
        // uses this to normalize the size scale so user-facing SizeScale is
        // a clean "x apparent-size multiplier" regardless of FBX mesh size.
        private static float s_meshNativeRadius;

        public static bool TryLoadAssets(AssetBundle bundle)
        {
            if (MoonShader != null && MoonPrefab != null && MoonTexture != null) return true;
            if (bundle == null) return false;

            if (MoonShader == null)
            {
                MoonShader = bundle.LoadAsset<Shader>(ShaderName);
                if (MoonShader == null)
                {
                    foreach (var s in bundle.LoadAllAssets<Shader>())
                        if (s != null && s.name == ShaderName) { MoonShader = s; break; }
                }
            }
            if (MoonPrefab == null)
            {
                MoonPrefab = bundle.LoadAsset<GameObject>(PrefabName);
                if (MoonPrefab == null)
                {
                    foreach (var go in bundle.LoadAllAssets<GameObject>())
                        if (go != null && go.name == PrefabName) { MoonPrefab = go; break; }
                }
            }
            if (MoonTexture == null)
            {
                MoonTexture = bundle.LoadAsset<Texture2D>(AlbedoName);
                if (MoonTexture == null)
                {
                    foreach (var t in bundle.LoadAllAssets<Texture2D>())
                        if (t != null && t.name == AlbedoName) { MoonTexture = t; break; }
                }
            }

            bool ok = MoonShader != null && MoonPrefab != null && MoonTexture != null;
            if (ok) FiresLogger.LogVerbose($"[FiresMoon] Loaded shader/prefab/texture from bundle.");
            return ok;
        }

        public static void Tick(Camera cam)
        {
            // Cross-mod master kill: FiresTossinShade's "00 Master" pushes _FiresMasterFxOff=1
            // when the user wants pure vanilla graphics. Stand down exactly like Enabled=false —
            // vanilla moon restored, our disc hidden. Unset/0 (FTS absent or master on) = run.
            if (!Enabled || Shader.GetGlobalFloat(s_propMasterFxOff) > 0.5f)
            {
                ApplyVanillaMoonSuppression(false);
                if (MoonInstance != null) MoonInstance.SetActive(false);
                return;
            }

            if (cam == null) return;
            if (MoonShader == null || MoonPrefab == null || MoonTexture == null) return;

            // We own "the moon" — keep Valheim's flat vanilla moon hidden either way.
            ApplyVanillaMoonSuppression(true);
            // Live brightness / blood-moon params, before the SuppressDisc return: the orbital moon shares this
            // material, so it has to stay current even while the disc itself is hidden.
            UpdateMaterialParams();

            // The reachable orbital moon (FiresOrbitals → FiresMoon prefab) is the visible moon,
            // so hide this skybox 3D disc to avoid two moons. Vanilla moon stays suppressed above,
            // so exactly one moon shows; tides/phase live in TideSystem and are unaffected.
            if (SuppressDisc)
            {
                if (MoonInstance != null && MoonInstance.activeSelf) MoonInstance.SetActive(false);
                return;
            }

            EnsureInstance(cam);
            UpdateMoonTransform(cam);
        }

        // Make the moon indestructible without disabling WearNTear, so its
        // network logic keeps working. The prefab ships with m_health = 1500
        // (vanilla floor default), which a determined player could pickaxe
        // through. Hits are stopped by max health plus Immune modifiers.
        // Wear ignores both: it deals a percentage of m_health, and an
        // unsupported piece takes 100% on its first tick, so every wear
        // source is switched off separately.
        private static void MakeMoonInvincible(GameObject root)
        {
            if (root == null) return;
            var wearComps = root.GetComponentsInChildren<WearNTear>(includeInactive: true);
            for (int i = 0; i < wearComps.Length; i++)
            {
                if (wearComps[i] == null) continue;
                wearComps[i].m_health = float.MaxValue;
                // The support/roof wear flags read backwards: false is what
                // turns that wear OFF, so a moon floating in space never crumbles.
                wearComps[i].m_noSupportWear    = false;
                wearComps[i].m_noRoofWear       = false;
                wearComps[i].m_ashDamageImmune  = true;
                wearComps[i].m_snowDamageImmune = true;
                // Damage resistances: everything goes to Immune so even
                // arrow-spam, fire, frost, lightning, etc. do nothing.
                wearComps[i].m_damages = new HitData.DamageModifiers
                {
                    m_blunt     = HitData.DamageModifier.Immune,
                    m_slash     = HitData.DamageModifier.Immune,
                    m_pierce    = HitData.DamageModifier.Immune,
                    m_chop      = HitData.DamageModifier.Immune,
                    m_pickaxe   = HitData.DamageModifier.Immune,
                    m_fire      = HitData.DamageModifier.Immune,
                    m_frost     = HitData.DamageModifier.Immune,
                    m_lightning = HitData.DamageModifier.Immune,
                    m_poison    = HitData.DamageModifier.Immune,
                    m_spirit    = HitData.DamageModifier.Immune,
                };
            }
        }

        // The prefab ships with a 2-LOD setup: LOD0 at screen-height 0.158,
        // LOD1 at 0.000115. That fades the moon out when its on-screen size
        // drops below ~0.01% — which is FINE for normal pieces but the moon
        // is rendered at 4500m+ on the skybox layer and SHOULD remain visible
        // at any apparent angular size. Push both screen-height thresholds
        // toward zero so the renderer ALWAYS draws, no matter how distant.
        // We DON'T destroy the LODGroup — the user authored it deliberately
        // and the same prefab needs to work as a placeable piece too. Just
        // make the thresholds permissive.
        private static void ExtendLODForeverAway(GameObject root)
        {
            if (root == null) return;
            var lodGroups = root.GetComponentsInChildren<LODGroup>(includeInactive: true);
            for (int i = 0; i < lodGroups.Length; i++)
            {
                if (lodGroups[i] == null) continue;
                var lods = lodGroups[i].GetLODs();
                int n = lods.Length;
                // Unity requires STRICTLY DESCENDING thresholds (each LOD's
                // value must be < previous one). All-zero is rejected with
                // "Attempting to set LOD where the screen relative size is
                // greater than or equal to a higher detail LOD level."
                //
                // Lerp from 0.5 (LOD0 takes over when moon is >50% of screen
                // — basically only at extreme close range) down to a value
                // tiny enough to never cull (0.0000001 = 1e-7). For a 2-LOD
                // setup this gives [0.5, 1e-7]: LOD1 renders almost always,
                // LOD0 only at point-blank, neither LOD ever culls. Result:
                // moon visible at ANY apparent angular size.
                for (int j = 0; j < n; j++)
                {
                    float t = (n <= 1) ? 1f : (float)j / (n - 1);
                    lods[j].screenRelativeTransitionHeight = Mathf.Lerp(0.5f, 1e-7f, t);
                }
                lodGroups[i].SetLODs(lods);
                lodGroups[i].size = 1000f;     // huge bounds so screen-relative-size never under-reports
                lodGroups[i].fadeMode = LODFadeMode.None;
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
        }

        // Idempotent layer assign — skips the recursive walk if root layer
        // already matches. Called every frame in VisitMode without overhead.
        private static void EnsureLayer(int layer)
        {
            if (MoonInstance == null) return;
            if (MoonInstance.layer == layer) return;
            SetLayerRecursively(MoonInstance, layer);
        }

        public static void RestoreImmediate()
        {
            ApplyVanillaMoonSuppression(false);
            if (MoonInstance != null)
            {
                Object.Destroy(MoonInstance);
                MoonInstance = null;
            }
        }

        private static void EnsureInstance(Camera cam)
        {
            if (MoonInstance != null && MoonInstance.activeSelf) return;
            if (MoonInstance == null)
            {
                MoonInstance = Object.Instantiate(MoonPrefab);
                MoonInstance.name = "FiresMoon (3D)";
                Object.DontDestroyOnLoad(MoonInstance);

                // Use the prefab AS-IS — keep Piece + ZNetView + BoxCollider
                // + MeshCollider + WearNTear intact. The collider is what
                // lets the player land/walk on the moon during va_goto_moon.
                // The Piece lets it be placed normally via Hammer. The
                // ZNetView lets multiplayer sync placement.
                //
                // We only adjust TWO things on the instance:
                //   1) WearNTear → invincible (no hit damage, no wear) so
                //      the moon can't be pickaxe-killed or crumble
                //   2) LODGroup minimum screen height → zero, so the moon
                //      keeps rendering at ANY apparent distance
                MakeMoonInvincible(MoonInstance);
                ExtendLODForeverAway(MoonInstance);

                SetLayerRecursively(MoonInstance, SkyboxLayer);

                // Build the runtime material that matches what the Unity
                // editor shows: Custom/FiresMoon shader + bundled FiresMoon_Albedo
                // texture. Doing this at runtime rather than trusting the
                // prefab's material reference because:
                //   * The prefab's material was cloned from AshWood_mat
                //     (the cloned 2×2 floor's material) and the cross-asset
                //     texture reference doesn't survive bundling reliably
                //   * At runtime the prefab's material slot ends up with
                //     no _MainTex bound → renderer shows just a pink/blue
                //     Lambertian gradient with NO craters
                // We assign this material to every renderer in the moon
                // hierarchy — this replaces the prefab's sharedMaterial
                // POINTER on the instance only; the bundle's material asset
                // is untouched, so a placed-via-Hammer VAFIREMOON piece
                // still uses the original material.
                int rendererCount = ApplyMoonLook(MoonInstance, out Bounds combined);

                // Cache native mesh radius (half of largest bounds dimension)
                // so UpdateMoonTransform can normalize the scale calculation
                // and SizeScale=1.0 means "vanilla-sized" regardless of how
                // big the FBX export was.
                Vector3 sz = combined.size;
                s_meshNativeRadius = Mathf.Max(sz.x, Mathf.Max(sz.y, sz.z)) * 0.5f;

                // Diagnostic: confirm the FBX actually has renderers + mesh
                // bounds. If rendererCount == 0 the FBX hierarchy is empty
                // and you'll see the "Instantiated" log but NOTHING in the
                // sky — that's the symptom to look for.
                FiresLogger.LogVerbose($"[FiresMoon] Instantiated 3D moon — renderers={rendererCount}, " +
                                       $"bounds.size={sz}, nativeRadius={s_meshNativeRadius:F2}, " +
                                       $"layer={MoonInstance.layer} ('skybox'={SkyboxLayer}), " +
                                       $"prefab='{MoonPrefab.name}', instance children={MoonInstance.transform.childCount}");
                if (rendererCount == 0)
                {
                    Debug.LogWarning("[FiresMoon] WARNING: FBX prefab has NO renderers in hierarchy. " +
                                     "The FBX import probably stripped them, or the mesh isn't on a " +
                                     "child GameObject with MeshFilter+MeshRenderer. Re-export FBX " +
                                     "with the moon mesh under a child node.");
                }
            }
            MoonInstance.SetActive(true);
        }

        /// <summary>
        /// Gives every renderer under <paramref name="root"/> the HD moon look: the runtime Custom/FiresMoon material with
        /// the bundled albedo (also pushed through a property block), no shadows, and LODs that never cull. Used by the
        /// sky disc AND by the reachable orbital moon (FiresOrbitals): the prefab's own material is a clone of a floor
        /// material whose texture reference doesn't survive bundling and whose shader resolves to Unity's 'Standard'
        /// (2026-10-05: the orbital moon logged firstShader='Standard' and showed no moon at night). Returns the
        /// renderer count; <paramref name="combined"/> is their world bounds.
        /// </summary>
        public static int ApplyMoonLook(GameObject root, out Bounds combined)
        {
            combined = new Bounds(root != null ? root.transform.position : Vector3.zero, Vector3.zero);
            if (root == null || MoonShader == null) return 0;
            if (MoonMaterial == null)
            {
                MoonMaterial = new Material(MoonShader) { name = MaterialName };
                MoonMaterial.SetTexture(Prop_MainTex, MoonTexture);
                UpdateMaterialParams();
            }

            var renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial    = MoonMaterial;
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows    = false;
                renderers[i].rendererPriority  = 1;
                if (i == 0) combined = renderers[i].bounds;
                else        combined.Encapsulate(renderers[i].bounds);

                renderers[i].GetPropertyBlock(block);
                if (MoonTexture != null)
                {
                    block.SetTexture("_MainTex", MoonTexture);
                    block.SetTexture("_BaseMap", MoonTexture);
                    block.SetTexture("_BaseColorMap", MoonTexture);
                }
                renderers[i].SetPropertyBlock(block);
            }
            ExtendLODForeverAway(root);
            return renderers.Length;
        }

        private static void UpdateMaterialParams()
        {
            if (MoonMaterial == null) return;
            MoonMaterial.SetFloat(Prop_SunBrightness,  SunBrightness);
            MoonMaterial.SetFloat(Prop_DarkSide,       DarkSide);
            MoonMaterial.SetFloat(Prop_Emission,       Emission);
            MoonMaterial.SetColor(Prop_EmissionTint,   EmissionTint);
            MoonMaterial.SetFloat(Prop_TerminatorSoft, TerminatorSoft);
            MoonMaterial.SetFloat(Prop_Saturation,     Saturation);
            MoonMaterial.SetFloat(Prop_BloodMoon,      Mathf.Clamp01(BloodMoon));
            MoonMaterial.SetColor(Prop_BloodColor,     BloodColor);
        }

        private static void ApplyVanillaMoonSuppression(bool hide)
        {
            // The aurora-skybox driver creates and owns the active skybox
            // material. We just set the global keyword/uniform on whatever
            // is RenderSettings.skybox right now so the suppression survives
            // EnvMan rewriting the skybox per env transition.
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty(Prop_HideVanilla))
                sky.SetFloat(Prop_HideVanilla, hide ? 1f : 0f);
        }

        // ===== Phase + position math =====
        // Returns moon direction (from camera) given the current sun direction
        // and game-time phase. Phase 0..1 maps to:
        //   0.00 = new moon  (moon ≈ sun direction, lit side faces away)
        //   0.25 = first quarter
        //   0.50 = full moon (moon at antipode, lit side faces camera)
        //   0.75 = last quarter
        //   1.00 = new moon (wraps)
        public static Vector3 ComputeMoonDirection(Vector3 sunDir, float phase01)
        {
            // Phase angle around the orbital plane. At phase 0.5 (full),
            // moon should be at -sunDir. So we use a rotation amount of
            // (phase01 - 0.5) * 360° applied to -sunDir around the orbital axis.
            //
            // Orbital axis: tilted by OrbitInclinationDeg from world up,
            // crossed with sunDir to get a stable per-frame axis. This way
            // the moon's orbit doesn't precess with sun motion.
            Vector3 sun        = sunDir.sqrMagnitude > 1e-6f ? sunDir.normalized : Vector3.right;
            float   incRad     = OrbitInclinationDeg * Mathf.Deg2Rad;
            Vector3 incAxis    = Vector3.Normalize(new Vector3(0, Mathf.Cos(incRad), Mathf.Sin(incRad)));
            // Axis perpendicular to both sun and the inclined "north" so
            // the orbital plane is roughly horizontal but tilted slightly.
            Vector3 orbitAxis  = Vector3.Cross(sun, incAxis);
            if (orbitAxis.sqrMagnitude < 1e-6f) orbitAxis = Vector3.up;
            orbitAxis.Normalize();

            float   angleDeg   = (phase01 - 0.5f) * 360f;
            Vector3 baseAntipode = -sun;
            return Quaternion.AngleAxis(angleDeg, orbitAxis) * baseAntipode;
        }

        public static float ComputePhase01()
        {
            // Manual preview override (va_moon_phase) wins over the game clock.
            if (PhaseOverride >= 0f) return Mathf.Repeat(PhaseOverride, 1f);

            // Use Valheim's day count + day fraction for smooth sub-day phase
            // progression. Phase resets every SynodicDays.
            try
            {
                if (EnvMan.instance == null) return 0.5f;  // fallback to full
                int   day      = EnvMan.instance.GetDay(ZNet.instance != null ? ZNet.instance.GetTimeSeconds() : 0);
                float dayFrac  = EnvMan.instance.GetDayFraction();
                float totalDay = day + dayFrac + PhaseOffsetDays;
                return Mathf.Repeat(totalDay / Mathf.Max(0.1f, SynodicDays), 1.0f);
            }
            catch { return 0.5f; }
        }

        private static void UpdateMoonTransform(Camera cam)
        {
            if (MoonInstance == null) return;

            Vector4 sunDirVec = Shader.GetGlobalVector("_SkyboxSunDir");
            Vector3 sunDir    = new Vector3(sunDirVec.x, sunDirVec.y, sunDirVec.z);
            if (sunDir.sqrMagnitude < 1e-6f) sunDir = Vector3.right;
            sunDir.Normalize();

            float   phase    = ComputePhase01();
            // Moon direction is SUN-RELATIVE: the antipode of the sun, walked around
            // the orbital plane by the phase angle. This is what keeps the moon
            // OPPOSITE the sun — it rises as the sun sets and crosses the night sky
            // tracking the anti-sun point — and it makes the phases correct, because
            // the lit face the shader builds from _SkyboxSunDir lines up with where
            // the moon actually sits. It also makes the moonlight reflection accurate
            // for free: EnvMan already points the night directional light from the
            // anti-sun direction, so the visible moon now sits exactly where its light
            // comes from. (The old FiresOrbitals egg-orbit ignored the sun entirely,
            // which is why moon, sun and moonlight all disagreed.)
            Vector3 moonDir  = ComputeMoonDirection(sunDir, phase);

            // VisitMode: moon stops camera-tracking and stays at the world
            // position recorded by va_goto_moon. Player can fly to it and
            // walk around. Skip the sky-tracking math entirely.
            //
            // CRITICAL: Vanilla SkyboxCamera (which renders layer 19) has
            // `near clip plane = 100`. Once the player is standing on or
            // near the moon, the moon is <100m from the camera and gets
            // NEAR-CLIPPED away — invisible. So we re-parent the moon to
            // the Default layer (0) for the main camera (near ~0.3m) to
            // render it. Layer flips back to 19 when VisitMode releases.
            Vector3 worldPos;
            if (VisitMode)
            {
                worldPos = VisitWorldPos;
                MoonInstance.transform.position = worldPos;
                // Scale to the physical visit radius so the player has a
                // tangible sphere to walk on, not an angular speck.
                float visitScale = VisitMoonRadius / Mathf.Max(0.001f, s_meshNativeRadius);
                MoonInstance.transform.localScale = new Vector3(visitScale, visitScale, visitScale);
                Vector3 toCam = (cam.transform.position - worldPos).normalized;
                MoonInstance.transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
                EnsureLayer(VisitLayer);
                return;
            }

            // Returning from VisitMode → restore skybox layer
            EnsureLayer(SkyboxLayer);

            worldPos = cam.transform.position + moonDir * Distance;
            MoonInstance.transform.position = worldPos;

            // Rotate the moon so its "north pole" axis stays consistent and
            // the visible face rotates slowly with the orbit (so the same
            // craters don't always face camera — gives a subtle natural feel).
            Vector3 northRef = Vector3.up;
            MoonInstance.transform.rotation = Quaternion.LookRotation(-moonDir, northRef);

            // Scale to achieve TargetAngularDiameterDeg apparent diameter
            // regardless of Distance. visualRadius is the world-space radius
            // the mesh needs to be to subtend that angle at this distance:
            //   tan(halfAngle) = radius / distance  →  radius = distance * tan(halfAngle)
            // The FBX mesh has its own native radius (we don't know exactly
            // what without inspecting bounds at runtime); instead of trusting
            // a constant, we read the renderer bounds from EnsureInstance and
            // normalize against them. If bounds wasn't captured (no renderer)
            // we fall back to scale=1 — driver logs warn in that case.
            float halfAngleRad   = TargetAngularDiameterDeg * 0.5f * Mathf.Deg2Rad;
            float visualRadius   = Distance * Mathf.Tan(halfAngleRad) * SizeScale;
            float meshRadius     = (s_meshNativeRadius > 0.001f) ? s_meshNativeRadius : 1f;
            float scale          = visualRadius / meshRadius;
            MoonInstance.transform.localScale = new Vector3(scale, scale, scale);
        }
    }

    public class FiresMoonDriver : MonoBehaviour
    {
        private void LateUpdate()
        {
            if (FiresMoon.MoonShader == null || FiresMoon.MoonPrefab == null || FiresMoon.MoonTexture == null)
            {
                // Galaxies owns the moon — load from its own firesgalaxies bundle
                // (no FiresTossinShade dependency).
                if (!FiresMoon.TryLoadAssets(VerdantsAscent.FiresValheimGalaxies.assetBundle)) return;
            }
            FiresMoon.Tick(Utils.GetMainCamera());
        }

        private void OnDisable() { FiresMoon.RestoreImmediate(); }
    }
}
