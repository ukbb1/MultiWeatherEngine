using System;
using SFS.Variables;
using SFS.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiWeatherEngine;

public class TyphoonHud : MonoBehaviour
{
	private GUIStyle box;

	private GUIStyle label;

	private GUIStyle small;

	private GUIStyle header;

	private Texture2D bg;

	private Texture2D bar;

	private Texture2D marker;

	private bool init;

	private WeatherSystem S => TyphoonManager.main != null ? TyphoonManager.main.selected : null;

	private void Setup()
	{
		init = true;
		
		bg = Solid(new Color(0.016f, 0.024f, 0.05f, 0.92f));
		bar = Solid(new Color(0.5f, 0.75f, 1f, 0.14f));
		marker = Solid(new Color(0.72f, 0.86f, 1f));
		box = new GUIStyle();
		box.normal.background = bg;
		box.padding = new RectOffset(12, 12, 10, 10);
		header = new GUIStyle();
		header.fontSize = 15;
		header.fontStyle = (FontStyle)1;
		header.normal.textColor = new Color(0.74f, 0.87f, 1f);
		label = new GUIStyle();
		label.fontSize = 13;
		label.normal.textColor = new Color(0.78f, 0.87f, 1f);
		small = new GUIStyle();
		small.fontSize = 11;
		small.normal.textColor = new Color(0.52f, 0.64f, 0.8f);
	}

	private static Texture2D Solid(Color c)
	{
		Texture2D val = new Texture2D(1, 1);
		val.SetPixel(0, 0, c);
		val.Apply();
		return val;
	}

	private void OnGUI()
	{
		if (!init)
		{
			Setup();
		}
		TyphoonManager main = TyphoonManager.main;
		if (main == null)
		{
			return;
		}
		if (TyphoonManager.GetPlayerLocation() == null && TyphoonManager.systems.Count == 0)
		{
			return;
		}
		if (TyphoonConfig.I.hud)
		{
			DrawBottomPanel(main);
			DrawTopBar(main);
		}
		
	}

	

	
	private void DrawBottomPanel(TyphoonManager main)
	{
		int n = TyphoonManager.systems.Count;
		bool exp = TyphoonManager.panelExpanded;
		float bw = Mathf.Min((float)Screen.width - 30f, 560f);
		float bh = exp ? (38f + (float)n * 24f + 8f) : 38f;
		if (n == 0)
		{
			bh = 38f;
		}
		float bx = 14f;
		float by = (float)Screen.height - bh - 14f;
		Rect panel = new Rect(bx, by, bw, bh);
		GUI.Box(panel, GUIContent.none, box);
		float x = bx + 10f;
		float y = by + 6f;
		GUIStyle hdr = new GUIStyle(GUI.skin.button);
		hdr.alignment = TextAnchor.MiddleLeft;
		hdr.fontSize = 13;
		hdr.fontStyle = FontStyle.Bold;
		hdr.normal.textColor = new Color(0.72f, 0.86f, 1f);
		if (GUI.Button(new Rect(x, y, bw - 20f, 24f), "🌪 活跃天气系统 (" + n + ")   " + (exp ? "▼ 点击收起" : "▶ 点击展开"), hdr))
		{
			TyphoonManager.panelExpanded = !TyphoonManager.panelExpanded;
		}
		if (n == 0)
		{
			GUI.Label(new Rect(x, y + 28f, bw - 20f, 18f), "无活跃系统 — [F6] 打开气象菜单召唤", small);
			return;
		}
		if (!exp)
		{
			string sum = "";
			for (int i = 0; i < n; i++)
			{
				WeatherSystem s = TyphoonManager.systems[i];
				if (s == null || !s.active)
				{
					continue;
				}
				if (sum.Length > 0)
				{
					sum += "  ·  ";
				}
				sum += WeatherSystem.TypeName(s.type) + " " + WeatherSystem.StrengthName(s.type, s.category);
			}
			GUI.Label(new Rect(x, y + 28f, bw - 20f, 18f), sum, small);
			return;
		}
		y += 30f;
		for (int i = 0; i < n; i++)
		{
			WeatherSystem s = TyphoonManager.systems[i];
			if (s == null || !s.active)
			{
				continue;
			}
			bool isSel = main.selected == s;
			GUIStyle row = new GUIStyle(isSel ? GUI.skin.box : GUI.skin.button);
			row.fontSize = 12;
			row.alignment = TextAnchor.MiddleLeft;
			row.normal.textColor = isSel ? new Color(0.58f, 0.88f, 1f) : new Color(0.82f, 0.9f, 1f);
			string stage = s.stage == 0 ? "发展" : (s.stage == 1 ? "成熟" : "消散");
			
			
			
			string stageTxt = stage + " " + (s.StageProgress() * 100.0).ToString("0") + "%";
			if (s.stage == 1)
			{
				stageTxt = "成熟 " + (s.StageProgress() * 100.0).ToString("0") + "%  升↗" + (s.naturalProgress * 100.0).ToString("0") + "%";
			}
			string phen = "";
			if (s.tornadoes.Count > 0)
			{
				
				phen += " 🌀龙卷×" + s.tornadoes.Count;
			}
			if (s.downbursts.Count > 0)
			{
				phen += " 💨下暴×" + s.downbursts.Count;
			}
			string txt = (isSel ? "▶ " : "  ") + WeatherSystem.TypeName(s.type) + "  " + WeatherSystem.StrengthName(s.type, s.category)
				+ "  [" + stageTxt + "]" + phen
				+ "  峰风 " + s.vmaxDisplay.ToString("0") + " m/s  半径 " + (s.Rmax / 1000.0).ToString("0.##") + " km  云顶 " + (s.Htop / 1000.0).ToString("0.0") + " km"
				+ DistTo(s);
			if (GUI.Button(new Rect(x, y, bw - 20f, 22f), txt, row))
			{
				main.selected = s;
				main.selectedType = (int)s.type;
			}
			y += 24f;
		}
	}

	private static string DistTo(WeatherSystem s)
	{
		try
		{
			Location pl = TyphoonManager.GetPlayerLocation();
			if (pl == null || pl.planet == null || (Object)pl.planet != (Object)s.planet)
			{
				return "  其他星球";
			}
			double d = Math.Abs(WrapAngle(s.centerAngle - pl.position.AngleRadians)) * s.planet.Radius;
			return "  距 " + (d / 1000.0).ToString("0.0") + " km";
		}
		catch
		{
			return "";
		}
	}

	private static double WrapAngle(double a)
	{
		while (a > Math.PI)
		{
			a -= Math.PI * 2.0;
		}
		while (a < -Math.PI)
		{
			a += Math.PI * 2.0;
		}
		return a;
	}

	
	private void DrawTopBar(TyphoonManager main)
	{
		WeatherSystem s = S;
		if (s == null || !s.active)
		{
			
			Rect v0 = new Rect(14f, 58f, 322f, 48f);
			GUI.Box(v0, GUIContent.none, box);
			GUI.Label(new Rect(28f, 64f, 300f, 20f), "TYPHOON — 待机  系统 " + TyphoonManager.systems.Count, header);
			GUI.Label(new Rect(28f, 86f, 460f, 18f), "[F6] 菜单  [Shift+F7] 隐藏", small);
			return;
		}
		float num = 250f;   
		
		Rect val = new Rect(14f, 58f, 322f, num);
		GUI.Box(val, GUIContent.none, box);
		float num2 = val.x + 14f;
		float num3 = val.y + 10f;
		Color val3 = (GUI.color = Category.Tint[s.category]);
		
		string phen = (s.tornadoes.Count > 0 ? "  龙卷×" + s.tornadoes.Count : "") + (s.downbursts.Count > 0 ? "  下暴×" + s.downbursts.Count : "");
		GUI.Label(new Rect(num2, num3, 322f, 20f), "◉ " + WeatherSystem.TypeName(s.type) + "  " + WeatherSystem.StrengthName(s.type, s.category) + "  [" + (s.stage == 0 ? "发展" : (s.stage == 1 ? "成熟" : "消散")) + "]" + phen, header);
		GUI.color = Color.white;
		num3 += 24f;
		GUI.Label(new Rect(num2, num3, 322f, 18f), "峰值风 " + s.vmaxDisplay.ToString("0") + " m/s (" + (s.vmaxDisplay * 3.6).ToString("0") + " km/h)   云底 " + (s.Hbase / 1000.0).ToString("0.00") + " km  云顶 " + (s.Htop / 1000.0).ToString("0.0") + " km  移速 " + s.drift.ToString("0.#") + " m/s", small);
		num3 += 20f;
		if ((Object)main != (Object)null && main.pValid)
		{
			double num4 = Math.Abs(main.pS);
			double rho = num4 / s.Rmax;
			double pU = main.pU;
			double pW = main.pW;
			double num5 = Math.Sqrt(pU * pU + pW * pW);
			GUI.Label(new Rect(num2, num3, 322f, 18f), "本地风  " + num5.ToString("0.0") + " m/s   (" + (num5 * 3.6).ToString("0") + " km/h)  = " + WeatherSystem.Beaufort(num5) + "级", label);
			num3 += 19f;
			GUI.Label(new Rect(num2, num3, 322f, 18f), "  水平 " + ((pU >= 0.0) ? "→ " : "← ") + Math.Abs(pU).ToString("0.0") + "    垂直 " + ((pW >= 0.0) ? "↑ " : "↓ ") + Math.Abs(pW).ToString("0.0") + " m/s", label);
			num3 += 19f;
			GUI.Label(new Rect(num2, num3, 322f, 18f), "真空速 " + main.pAirspeed.ToString("0.0") + " m/s    高度 " + (main.pH / 1000.0).ToString("0.00") + " km", label);
			num3 += 19f;
			
			
			if (s.type == StormType.Typhoon)
			{
				GUI.Label(new Rect(num2, num3, 322f, 18f), "距风眼 " + (num4 / 1000.0).ToString("0.0") + " km   (ρ=" + rho.ToString("0.00") + ")  " + Zone(rho, s, main.pS), label);
				num3 += 19f;
				
				double cr7 = s.WindCircleRo(13.9);
				double cr10 = s.WindCircleRo(24.5);
				double cr12 = s.WindCircleRo(32.7);
				GUI.Label(new Rect(num2, num3, 322f, 18f), "风圈 7级≈" + (cr7 * s.Rmax / 1000.0).ToString("0") + "km  10级≈" + (cr10 * s.Rmax / 1000.0).ToString("0") + "km  12级≈" + (cr12 * s.Rmax / 1000.0).ToString("0") + "km", label);
				num3 += 19f;
			}
			else
			{
				GUI.Label(new Rect(num2, num3, 322f, 18f), "距中心 " + (num4 / 1000.0).ToString("0.0") + " km   (ρ=" + rho.ToString("0.00") + ")  风级 " + WeatherSystem.Beaufort(num5) + "级", label);
				num3 += 19f;
			}
			num3 += 22f;
			
			
			float num6 = 150f;
			float num7 = 12f;
			Rect val5 = new Rect(num2 + 294f - num6, num3, num6, num7);
			GUI.DrawTexture(val5, (Texture)bar);
			
			
			if (s.type == StormType.Typhoon)
			{
				for (int i = -1; i <= 1; i += 2)
				{
					float num8 = val5.x + num6 * 0.5f + (float)i * (float)(s.Rmax / (s.Router * 1.6) * (double)num6 * 0.5);
					GUI.color = new Color(val3.r, val3.g, val3.b, 0.85f);
					GUI.DrawTexture(new Rect(num8 - 1f, val5.y, 2f, num7), (Texture)marker);
				}
			}
			GUI.color = new Color(1f, 1f, 1f, 0.35f);
			GUI.DrawTexture(new Rect(val5.x + num6 * 0.5f - 1f, val5.y, 2f, num7), (Texture)marker);
			float num11 = Mathf.Clamp((float)(main.pS / (s.Router * 1.6)), -1f, 1f);
			GUI.color = Color.white;
			GUI.DrawTexture(new Rect(val5.x + num6 * 0.5f + num11 * num6 * 0.5f - 2f, val5.y - 3f, 4f, num7 + 6f), (Texture)marker);
			GUI.color = Color.white;
			num3 += num7 + 6f;
		}
		else
		{
			GUI.Label(new Rect(num2, num3, 322f, 18f), "（不在该星球 / 风暴范围外）", label);
			num3 += 80f;
		}
		
		GUI.Label(new Rect(num2, num3, 620f, 18f), "[F6] 菜单  [F7] 解散  [F8] 强度  [F9] 面板  [Shift+F7] 隐藏", small);
		num3 += 19f;
	}

	private static double ViewDist()
	{
		try
		{
			return ((Obs<float>)(object)WorldView.main.viewDistance).Value;
		}
		catch
		{
			return 0.0;
		}
	}

	private static string Zone(double rho, WeatherSystem s, double pS)
	{
		
		
		
		
		double sWind = pS - (pS >= 0.0 ? (double)StormRenderer.windZoneOffA : (double)StormRenderer.windZoneOffB) * s.Rmax;
		int zi = WeatherSystem.WindZoneIndex(sWind / s.Rmax);
		return "[" + zi + "]" + StormRenderer.windZoneNames[zi];
	}
}
