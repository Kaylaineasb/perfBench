using System;
using UnityEngine;

namespace PerfBench
{
    public struct BatterySample
    {
        public bool valid;
        public int levelPct;            // BATTERY_PROPERTY_CAPACITY
        public long currentNowRaw;      // BATTERY_PROPERTY_CURRENT_NOW (bruto: sinal/unidade variam por fabricante)
        public long chargeCounterUah;   // BATTERY_PROPERTY_CHARGE_COUNTER (µAh)
        public float temperatureC;      // EXTRA_TEMPERATURE / 10
        public int voltageMv;
        public int plugged;             // 0 = na bateria; >0 = carregando (USB/AC/sem fio)
        public int thermalStatus;       // PowerManager.getCurrentThermalStatus (API 29+), -1 = n/d
        public float thermalHeadroom;   // PowerManager.getThermalHeadroom (API 30+), NaN = n/d

        public static BatterySample Invalid => new BatterySample
        {
            levelPct = -1, currentNowRaw = long.MinValue, chargeCounterUah = long.MinValue,
            temperatureC = float.NaN, voltageMv = -1, plugged = -1, thermalStatus = -1, thermalHeadroom = float.NaN
        };
    }

    /// <summary>
    /// Leitura de bateria/temperatura/estado térmico via JNI. Amostrar a ~1 Hz.
    /// </summary>
    public sealed class BatteryProbe
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject context, batteryManager, powerManager;
        int sdk;
        float lastHeadroom = float.NaN;
        float lastHeadroomTime = -100f;
#endif

        public BatteryProbe()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    context = up.GetStatic<AndroidJavaObject>("currentActivity");
                batteryManager = context.Call<AndroidJavaObject>("getSystemService", "batterymanager");
                powerManager = context.Call<AndroidJavaObject>("getSystemService", "power");
                using (var ver = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdk = ver.GetStatic<int>("SDK_INT");
            }
            catch (Exception e) { Debug.LogWarning("[PERFBENCH] BatteryProbe: " + e.Message); }
#endif
        }

        public BatterySample Sample()
        {
            var s = BatterySample.Invalid;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (batteryManager == null) return s;
            try
            {
                s.levelPct = batteryManager.Call<int>("getIntProperty", 4);
                s.currentNowRaw = batteryManager.Call<int>("getIntProperty", 2);
                s.chargeCounterUah = batteryManager.Call<int>("getIntProperty", 1);
                ReadBatteryIntent(ref s);
                if (powerManager != null && sdk >= 29)
                    s.thermalStatus = powerManager.Call<int>("getCurrentThermalStatus");
                // getThermalHeadroom pode devolver NaN se chamado com muita frequência.
                if (powerManager != null && sdk >= 30 && Time.realtimeSinceStartup - lastHeadroomTime >= 5f)
                {
                    float h = powerManager.Call<float>("getThermalHeadroom", 10);
                    if (!float.IsNaN(h)) lastHeadroom = h;
                    lastHeadroomTime = Time.realtimeSinceStartup;
                }
                s.thermalHeadroom = lastHeadroom;
                s.valid = true;
            }
            catch (Exception e) { Debug.LogWarning("[PERFBENCH] Battery sample: " + e.Message); }
#endif
            return s;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // registerReceiver(null, ACTION_BATTERY_CHANGED) devolve o intent "sticky" com
        // temperatura/tensão/carregador. JNI cru porque AndroidJavaObject não aceita null tipado.
        void ReadBatteryIntent(ref BatterySample s)
        {
            using (var filter = new AndroidJavaObject("android.content.IntentFilter", "android.intent.action.BATTERY_CHANGED"))
            {
                IntPtr ctx = context.GetRawObject();
                IntPtr ctxClass = AndroidJNI.GetObjectClass(ctx);
                IntPtr mid = AndroidJNI.GetMethodID(ctxClass, "registerReceiver",
                    "(Landroid/content/BroadcastReceiver;Landroid/content/IntentFilter;)Landroid/content/Intent;");
                AndroidJNI.DeleteLocalRef(ctxClass);
                if (mid == IntPtr.Zero) { ClearJniException(); return; }

                var args = new jvalue[2];
                args[0].l = IntPtr.Zero;
                args[1].l = filter.GetRawObject();
                IntPtr intent = AndroidJNI.CallObjectMethod(ctx, mid, args);
                if (ClearJniException() || intent == IntPtr.Zero) return;

                IntPtr intentClass = AndroidJNI.GetObjectClass(intent);
                IntPtr getIntExtra = AndroidJNI.GetMethodID(intentClass, "getIntExtra", "(Ljava/lang/String;I)I");
                if (getIntExtra != IntPtr.Zero)
                {
                    int t = GetIntExtra(intent, getIntExtra, "temperature", int.MinValue);
                    s.temperatureC = t == int.MinValue ? float.NaN : t / 10f;
                    s.voltageMv = GetIntExtra(intent, getIntExtra, "voltage", -1);
                    s.plugged = GetIntExtra(intent, getIntExtra, "plugged", -1);
                }
                ClearJniException();
                AndroidJNI.DeleteLocalRef(intentClass);
                AndroidJNI.DeleteLocalRef(intent);
            }
        }

        static int GetIntExtra(IntPtr intent, IntPtr mid, string key, int def)
        {
            IntPtr jkey = AndroidJNI.NewStringUTF(key);
            var a = new jvalue[2];
            a[0].l = jkey;
            a[1].i = def;
            int v = AndroidJNI.CallIntMethod(intent, mid, a);
            AndroidJNI.DeleteLocalRef(jkey);
            return ClearJniException() ? def : v;
        }

        static bool ClearJniException()
        {
            IntPtr ex = AndroidJNI.ExceptionOccurred();
            if (ex == IntPtr.Zero) return false;
            AndroidJNI.ExceptionClear();
            AndroidJNI.DeleteLocalRef(ex);
            return true;
        }
#endif
    }
}
