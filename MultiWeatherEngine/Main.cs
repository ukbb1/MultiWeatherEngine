using System;
using System.Reflection;
using HarmonyLib;
using ModLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiWeatherEngine;

public class Main : Mod
{
	public static Main main;

	public static Harmony harmony;

	public override string ModNameID => "MultiWeatherEngine";

	public override string DisplayName => "多天气系统引擎";

	public override string Author => "星程（AI 制作）";

	public override string MinimumGameVersionNecessary => "1.6";

	public override string ModVersion => "v2.1.0";

	public override string Description => "多天气系统引擎（星程气象）：台风、超级单体、飑线等 6 类天气系统自然生成、演化、移动、合并与类型转变，真实风场/降雨/龙卷/下击暴流，全程可交互。v2.1.0 台风生命周期（用户：现实里终究发展成熟消散，0=持续不行）：台风 lifetimeSec 0→259200（3 天，现实 3-7 天），走完整时间制演化——发展 1-2 分钟 → 成熟（自然升级）→ 消散，消散后自然生成再造新台风。v2.0.99 保留：代码体检修复（台风 stage/合并增强/时间加速同步 dt 30s/粒子采样缓存+三层冻结）。v2.0.98 保留：强度平滑/自然升级/类型转变/附属跟母体。双层漏斗壁实芯透、龙卷向心吸、F6 指挥中心、深空色系。本 mod 由 AI 辅助设计制作。";

	public override void Early_Load()
	{
		
		
		main = this;
		try
		{
			harmony = new Harmony("workbuddy.multiweatherengine");
			harmony.PatchAll(Assembly.GetExecutingAssembly());
			Debug.Log((object)"[Typhoon] Harmony patches applied.");
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[Typhoon] Harmony patch failed: " + ex));
		}
	}

	public override void Load()
	{
		
		
		
		
		
		try
		{
			TyphoonConfig.Load(((Mod)this).ModFolder);
		}
		catch (Exception ex)
		{
			Debug.LogError((object)("[Typhoon] config load failed: " + ex));
		}
		GameObject val = new GameObject("Typhoon Manager");
		Object.DontDestroyOnLoad((Object)val);
		val.AddComponent<TyphoonManager>();
		Debug.Log((object)("[Typhoon] " + ((Mod)this).ModVersion + " loaded. Press F6 in a world to open the weather menu."));
	}
}
