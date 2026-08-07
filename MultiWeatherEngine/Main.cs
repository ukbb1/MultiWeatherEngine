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

	public override string ModVersion => "v2.0.98";

	public override string Description => "多天气系统引擎（星程气象）：台风、超级单体、飑线等 6 类天气系统自然生成、演化、移动、合并与类型转变，真实风场/降雨/龙卷/下击暴流，全程可交互。v2.0.98 十二项：①F8 强度切换粒子过渡（vmaxTarget 平滑逼近 ~3 秒）+ 附属现象强度跟母体（0.5+cat/6，Shift+F8 龙卷Lv 删除）；②自然发展强度进度条（成熟期升↗XX%，满自然升级一级，粒子过渡）；③性能优化——只对正在影响飞船的风暴实时补充/删除粒子，其他风暴粒子冻结只移动、保留打雷；④系统生成动画（云粒子 2 秒渐入）；⑤自然类型转变（单体→多单体→超级单体→MCS、飑线→MCS，3 秒粒子过渡、原基础上转变）；⑥F1 参数编辑菜单删除；⑦HUD 删视距/缩放空间/龙卷Lv 字样；⑧HUD 下移（y 58）；⑨活跃系统 bar 左边缘缩减一半、右边缘固定；⑩键位重排（F6 菜单/F7 解散/F8 强度/F9 面板/Shift+F7 隐藏）；⑪模组介绍最大化精简。v2.0.97 保留：台风眼壁统一/保底修正。双层漏斗壁实芯透、龙卷向心吸、F6 指挥中心、深空色系。本 mod 由 AI 辅助设计制作。";

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
		Debug.Log((object)("[Typhoon] " + ((Mod)this).ModVersion + " loaded. Press F7 in a world to spawn a storm."));
	}
}
