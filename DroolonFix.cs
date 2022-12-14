using BaseX;
using FrooxEngine;
using FrooxEngine.CommonAvatar;
using HarmonyLib;
using NeosModLoader;
using System.Reflection;
using static FrooxEngine.CommonAvatar.AvatarRawEyeData;

namespace DroolonFix
{

    public class DroolonFix : NeosMod
    {

        public override string Name => "DroolonFix";
        public override string Author => "portalsam";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/portalsam1/DroolonFix";

        public static ModConfiguration config;

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<bool> enabled = new ModConfigurationKey<bool>("Enabled", "Enable eye tracking fix.", () => true);

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<float> leftEyeMaxInput = new ModConfigurationKey<float>("Left Eye Max Input", "How much input from the eyetracker to fully close the left eye.", () => 0.48f);

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<float> rightEyeMaxInput = new ModConfigurationKey<float>("Right Eye Max Input", "How much input from the eyetracker to fully close the right eye.", () => 0.48f);

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<float> eyeSmoothing = new ModConfigurationKey<float>("Eye Smoothing", "Smoothing amount of the eye movement.", () => 0.15f);

        [AutoRegisterConfigKey]
        public static ModConfigurationKey<float> verticalOffset = new ModConfigurationKey<float>("Vertical Offset", "Vertical Eye Offset that may be needed for some avatars.", () => 0f);

        public static Slot dataSlot = null, leftSlot = null, rightSlot = null;
        public static LookAt leftLook, rightLook;
        public static AvatarRawEyeData currentEyeDataSource = null;
        public static EyeManager currentEyeManager = null;

        public override void OnEngineInit()
        {

            config = GetConfiguration();
            config.Save();

            Harmony harmony = new Harmony("net.portalsam.DroolonFix");
            harmony.PatchAll();

        }

        [HarmonyPatch(typeof(EyeLinearDriver), "OnCommonUpdate")]
        class EyeLinearDriverPatch
        {
            static void Postfix(EyeLinearDriver __instance)
            {

                if (!config.GetValue(enabled)) return;

                if (__instance.Slot.Find("DroolonFix.EyeData") == null)
                {

                    currentEyeManager = __instance.EyeManager.Target;

                    SyncList<EyeLinearDriver.Eye> eyeList = (SyncList<EyeLinearDriver.Eye>)__instance.GetSyncMember(13);
                    foreach (EyeLinearDriver.Eye eye in eyeList.Elements)
                    {
                        switch (eye.Side.Value)
                        {
                            case EyeSide.Left:
                                eye.MaxInputCloseness.ForceSet(config.GetValue(leftEyeMaxInput));
                                break;
                            case EyeSide.Right:
                                eye.MaxInputCloseness.ForceSet(config.GetValue(rightEyeMaxInput));
                                break;
                            default: break;
                        }
                    }

                    dataSlot = __instance.Slot.AddSlot("DroolonFix.EyeData", false);
                    dataSlot.LocalPosition = dataSlot.LocalPosition + (float3.Up * config.GetValue(verticalOffset)); 

                    currentEyeDataSource = dataSlot.AttachComponent<AvatarRawEyeData>();
                    typeof(AvatarRawEyeData).GetField("_activeUser", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(currentEyeDataSource, currentEyeManager.SimulatingUser);

                }

                if (leftSlot.IsDestroyed || rightSlot.IsDestroyed || leftSlot == null || rightSlot == null) return;

                EyeData leftEyeData = currentEyeDataSource.LeftEye;
                leftSlot.LocalPosition = MathX.Lerp(leftSlot.LocalPosition, leftEyeData.Direction.Value - leftEyeData.Origin.Value, config.GetValue(eyeSmoothing));

                EyeData rightEyeData = currentEyeDataSource.RightEye;
                rightSlot.LocalPosition = MathX.Lerp(rightSlot.LocalPosition, rightEyeData.Direction.Value - rightEyeData.Origin.Value, config.GetValue(eyeSmoothing));

            }

        }

        [HarmonyPatch(typeof(EyeRotationDriver), "OnCommonUpdate")]
        class EyeRotationDriverPatch
        {
            static bool Prefix(EyeRotationDriver __instance)
            {

                if (!config.GetValue(enabled)) return false;
                if (currentEyeDataSource == null) return true;

                if (dataSlot.Find("DroolonFix.Left") == null && !dataSlot.IsDestroyed && dataSlot != null)
                {

                    leftSlot = dataSlot.AddSlot("DroolonFix.Left", false);

                    foreach (EyeRotationDriver.Eye eye in __instance.Eyes)
                    {
                        if (eye.IsValidEye && eye.Side == EyeSide.Left)
                        {

                            leftLook = __instance.Slot.Parent.AttachComponent<LookAt>();
                            leftLook.Enabled = false;

                            FieldDrive<floatQ> target = (FieldDrive<floatQ>)typeof(LookAt).GetField("_target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(leftLook);
                            IField<floatQ> eyeRotationField = eye.Rotation.Target;

                            eye.Rotation.ReleaseLink();
                            target.ReleaseLink();
                            target.ForceLink(eyeRotationField);

                            leftLook.Target.Target = leftSlot;
                            leftLook.Enabled = true;

                        }
                    }

                }
                if (dataSlot.Find("DroolonFix.Right") == null && !dataSlot.IsDestroyed && dataSlot != null)
                {

                    rightSlot = dataSlot.AddSlot("DroolonFix.Right", false);

                    foreach (EyeRotationDriver.Eye eye in __instance.Eyes)
                    {
                        if (eye.IsValidEye && eye.Side == EyeSide.Right)
                        {

                            rightLook = __instance.Slot.Parent.AttachComponent<LookAt>();
                            rightLook.Enabled = false;

                            FieldDrive<floatQ> target = (FieldDrive<floatQ>)typeof(LookAt).GetField("_target", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(rightLook);
                            IField<floatQ> eyeRotationField = eye.Rotation.Target;

                            eye.Rotation.ReleaseLink();
                            target.ReleaseLink();
                            target.ForceLink(eyeRotationField);

                            rightLook.Target.Target = rightSlot;
                            rightLook.Enabled = true;

                        }
                    }
                }

                return true;


            }
        }

    }
}