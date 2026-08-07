using System;
using System.Collections.Generic;
using SFS;
using SFS.UI;
using SFS.Variables;
using SFS.World;
using SFS.WorldBase;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiWeatherEngine;

public class TyphoonManager : MonoBehaviour
{
	public static TyphoonManager main;

	public static List<WeatherSystem> systems = new List<WeatherSystem>();

	private readonly List<StormRenderer> renderers = new List<StormRenderer>();

	public WeatherSystem selected;

	public int selectedType = 0;

	public bool menuOpen;

	
	public static bool panelExpanded = true;

	
	private static Vector2 menuOffset = Vector2.zero;
	private static bool menuDragging;
	private static Vector2 menuGrabDelta;

	private double lastWorldTime = double.NaN;

	private float shakeCooldown;

	private float spawnTimer = 25f;

	private const int MaxSystems = 10;

	private TyphoonHud hud;

	public double pS;

	public double pH;

	public double pU;

	public double pW;

	public double pAirspeed;

	public bool pValid;

	
	public bool diagPValid;
	public string diagType = "-";
	public double diagRo = -1.0;
	public double diagHkm;
	public double diagStrT;
	public double diagStrD;
	public double diagU;
	public double diagW;
	public double diagWindSum;
	
	public double diagDro;
	public double diagDspd;
	private float lastDLog = -99f;   

	private void Awake()
	{
		main = this;
		hud = ((Component)this).gameObject.AddComponent<TyphoonHud>();
	}

	public static Location ToAirspeedFrame(Location loc)
	{
		try
		{
			if (loc == null || (Object)loc.planet == (Object)null)
			{
				return loc;
			}
			Double2 wind = Double2.zero;
			bool any = false;
			for (int i = 0; i < systems.Count; i++)
			{
				WeatherSystem sys = systems[i];
				if (sys == null || !sys.active || (Object)sys.planet == (Object)null || (Object)(object)sys.planet != (Object)(object)loc.planet)
				{
					continue;
				}
				Double2 v = sys.SampleWind(loc.position);
				if (v.x != 0.0 || v.y != 0.0)
				{
					wind += v;
					any = true;
				}
			}
			if (!any)
			{
				return loc;
			}
			return new Location(loc.time, loc.planet, loc.position, loc.velocity - wind);
		}
		catch
		{
			return loc;
		}
	}

	private void Update()
	{
		HandleInput();
		AdvanceSystems();
		NaturalSpawn();
		TryMergeAll();
		ProbePlayer();
		ApplyShake();
	}

	
	private void LateUpdate()
	{
		BoostWaves();
		ApplyProximityFX();
	}

	
	
	private float ProximityFactor()
	{
		if (!pValid)
		{
			return 0f;
		}
		try
		{
			double vd = 0.0;
			try
			{
				vd = ((Obs<float>)(object)WorldView.main.viewDistance).Value;
			}
			catch
			{
			}
			float vdF = Mathf.Clamp01(1f - (float)(vd / 8000.0));
			if (vdF <= 0.01f)
			{
				return 0f;
			}
			Location pl = GetPlayerLocation();
			if (pl == null || pl.planet == null)
			{
				return 0f;
			}
			double bestRo = 1e9;
			double bestV = 0.0;
			StormType bestType = StormType.Cell;
			int bestCat = 0;
			for (int i = 0; i < systems.Count; i++)
			{
				WeatherSystem s = systems[i];
				if (s == null || !s.active || (Object)s.planet == (Object)null || (Object)s.planet != (Object)pl.planet)
				{
					continue;
				}
				double da = s.centerAngle - pl.position.AngleRadians;
				while (da > Math.PI)
				{
					da -= Math.PI * 2.0;
				}
				while (da < -Math.PI)
				{
					da += Math.PI * 2.0;
				}
				double ro = Math.Abs(da) * s.planet.Radius / s.Rmax;
				if (ro < bestRo)
				{
					bestRo = ro;
					bestV = s.Vmax;
					bestType = s.type;
					bestCat = s.category;
				}
			}
			if (bestRo > 1e8)
			{
				return 0f;
			}
			
			
			if (bestType == StormType.Typhoon && bestRo < WeatherSystem.TyphoonEyeR(bestCat))
			{
				return 0.02f;
			}
			float roF = Mathf.Clamp01(1f - (float)(bestRo / 1.5));
			float strF = Mathf.Clamp01((float)(bestV / 60.0));
			return Mathf.Clamp01(vdF * roF * strF * 1.4f);
		}
		catch
		{
			return 0f;
		}
	}

	
	
	
	
	
	private void ApplyProximityFX()
	{
		float f = ProximityFactor();
		if (f < 0.02f)
		{
			return;
		}
		try
		{
			if (WorldView.main == null || WorldView.main.postProcessing == null || WorldView.main.postProcessing.Length == 0)
			{
				return;
			}
			PostProcessing pp = WorldView.main.postProcessing[0];
			if (pp == null || pp.postProcessingMaterial == null)
			{
				return;
			}
			Material m = pp.postProcessingMaterial;
			
			
			float g = Mathf.Clamp01(f * 3f);
			
			
			
			double hDark = 0.0;
			try
			{
				Location pl2 = GetPlayerLocation();
				if (pl2 != null && pl2.planet != null && pH > 2000.0)
				{
					WeatherSystem ns = null;
					double nRo = 1e9;
					for (int i = 0; i < systems.Count; i++)
					{
						WeatherSystem s = systems[i];
						if (s == null || !s.active || s.planet == null || (Object)s.planet != (Object)pl2.planet)
						{
							continue;
						}
						double da = s.centerAngle - pl2.position.AngleRadians;
						while (da > Math.PI)
						{
							da -= Math.PI * 2.0;
						}
						while (da < -Math.PI)
						{
							da += Math.PI * 2.0;
						}
						double ro = Math.Abs(da) * s.planet.Radius;
						if (ro < nRo)
						{
							nRo = ro;
							ns = s;
						}
					}
					if (ns != null && ns.Htop > 2100.0)
					{
						double peak = (ns.Hbase + ns.Htop) * 0.5;
						double end = ns.Htop * 1.15;
						if (pH < peak)
						{
							hDark = Mathf.Clamp01((float)((pH - 2000.0) / Math.Max(peak - 2000.0, 1.0)));
						}
						else
						{
							hDark = Mathf.Clamp01((float)(1.0 - (pH - peak) / Math.Max(end - peak, 1.0)));
						}
					}
				}
			}
			catch
			{
			}
			float g2 = Mathf.Clamp01(g + (float)hDark * 0.6f);   
			m.SetFloat(Shader.PropertyToID("_Saturation"), Mathf.Lerp(1f, 0.15f, g2));
			m.SetFloat(Shader.PropertyToID("_Contrast"), Mathf.Lerp(1f, 0.7f, g2));
			
			
			
			
			
			
			float lum = Mathf.Lerp(1f, 0.62f, g) * (1f - (float)hDark * 0.7f);
			float bLum = lum * (1f - (float)hDark * 0.45f) * (1f - g * 0.28f);
			m.SetVector(Shader.PropertyToID("_Multiplier"), new Vector4(lum, lum, bLum, 1f));
		}
		catch
		{
		}
	}

	
	private void BoostWaves()
	{
		if (!pValid)
		{
			return;
		}
		try
		{
			if (WorldView.main == null)
			{
				return;
			}
			Planet planet = WorldView.main.ViewLocation.planet;
			if (planet == null || planet.waterMaterial == null)
			{
				return;
			}
			double wind = Math.Sqrt(pU * pU + pW * pW);
			float f = (float)Math.Min(wind / 30.0, 1.0);
			planet.waterMaterial.SetFloat(Shader.PropertyToID("_WaveHeight"), 0.02f + 0.2f * f);
		}
		catch
		{
		}
	}

	private void AdvanceSystems()
	{
		WorldTime val = WorldTime.main;
		if ((Object)val == (Object)null)
		{
			return;
		}
		double worldTime = val.worldTime;
		if (double.IsNaN(lastWorldTime))
		{
			lastWorldTime = worldTime;
			return;
		}
		double dt = worldTime - lastWorldTime;
		lastWorldTime = worldTime;
		for (int i = systems.Count - 1; i >= 0; i--)
		{
			WeatherSystem sys = systems[i];
			if (sys == null || !sys.active)
			{
				RemoveSystemAt(i);
				continue;
			}
			sys.Advance(dt);
			if (!sys.active)
			{
				Msg(WeatherSystem.TypeName(sys.type) + " 已消散（生命周期结束）");
				RemoveSystemAt(i);
			}
		}
	}

	
	private void NaturalSpawn()
	{
		if (systems.Count >= MaxSystems)
		{
			return;
		}
		spawnTimer -= Time.deltaTime;
		if (spawnTimer > 0f)
		{
			return;
		}
		spawnTimer = 30f + UnityEngine.Random.Range(0f, 40f);
		Location loc = GetPlayerLocation();
		if (loc == null || (Object)loc.planet == (Object)null || !loc.planet.HasAtmospherePhysics)
		{
			return;
		}
		if (UnityEngine.Random.value > 0.6f)
		{
			return;
		}
		StormType t = (UnityEngine.Random.value < 0.55f) ? StormType.Cell : ((UnityEngine.Random.value < 0.7f) ? StormType.Multicell : StormType.Supercell);
		double lead = (double)(12000f + UnityEngine.Random.Range(0f, 50000f)) * (UnityEngine.Random.value < 0.5f ? 1.0 : -1.0);
		SpawnSystem(t, loc, lead, 0, true);   
	}

	
	private void TryMergeAll()
	{
		for (int i = 0; i < systems.Count; i++)
		{
			for (int j = i + 1; j < systems.Count; j++)
			{
				WeatherSystem a = systems[i];
				WeatherSystem b = systems[j];
				if (a == null || b == null || !a.active || !b.active || (Object)(object)a.planet != (Object)(object)b.planet)
				{
					continue;
				}
				double ang = Math.Abs(WeatherSystem.WrapPi(a.centerAngle - b.centerAngle));
				double dist = ang * a.planet.Radius;
				
				
				
				
				double mergeDist;
				if (a.type == StormType.Typhoon)
				{
					mergeDist = a.Router * 0.9;
				}
				else if (b.type == StormType.Typhoon)
				{
					mergeDist = b.Router * 0.9;
				}
				else
				{
					mergeDist = (a.Rmax + b.Rmax) * 1.2;
				}
				if (dist > mergeDist)
				{
					continue;
				}
				WeatherSystem big = (a.Rmax >= b.Rmax) ? a : b;
				WeatherSystem small = (a.Rmax >= b.Rmax) ? b : a;
				
				if (small.mergeAnimT >= 0.0 || big.mergeAnimT >= 0.0)
				{
					continue;
				}
				
				small.mergeTarget = big;
				small.mergeMode = (big.type == StormType.Typhoon) ? 1 : 0;   
				small.mergeAnimT = 0.0;
				if (big.type == StormType.Typhoon && small.type == StormType.Typhoon)
				{
					
					small.mergeMode = 2;
					big.mergeMode = 2;
					big.mergeTarget = small;
					big.mergeAnimT = 0.0;
					big.mergeMidAng = small.mergeMidAng = WeatherSystem.WrapPi((big.centerAngle + small.centerAngle) * 0.5);
				}
				
				big.mergeCount++;
				double gain = 1.0 / (1.0 + big.mergeCount * 0.6);
				big.Rmax = Math.Min(big.Rmax * (1.0 + 0.12 * gain), big.rmaxBase * 3.0);
				big.Router = big.Rmax * 9.0;
				big.Vmax = Math.Min(big.Vmax * (1.0 + 0.08 * gain), big.vmaxBase * 2.0);
				
				
				big.vmaxTarget = big.Vmax;
				big.intensity = 1.0;
				big.stage = 1;
				Msg(WeatherSystem.TypeName(small.type) + " 被 " + WeatherSystem.TypeName(big.type) + " 吞并（合并增强 #" + big.mergeCount + "）");
				break;
			}
		}
		
		for (int k = systems.Count - 1; k >= 0; k--)
		{
			WeatherSystem s = systems[k];
			if (s.mergeAnimT >= 1.0)
			{
				if (s.mergeMode == 2)
				{
					s.mergeAnimT = -1.0;
					s.mergeTarget = null;
				}
				else
				{
					RemoveSystem(s);
				}
			}
		}
	}

	private void HandleInput()
	{
		bool flag = Input.GetKey((KeyCode)304) || Input.GetKey((KeyCode)303);
		if (Input.GetKeyDown((KeyCode)288)) 
		{
			if (flag)
			{
				TyphoonConfig.I.hud = !TyphoonConfig.I.hud;
				return;
			}
			if (selected != null && selected.active)
			{
				RemoveSystem(selected);
				selected = null;
				Msg("已解散选中的天气系统");
			}
			return;
		}
		if (Input.GetKeyDown((KeyCode)287)) 
		{
			menuOpen = !menuOpen;
			Msg("气象菜单 " + (menuOpen ? "开" : "关") + "（F6）");
			return;
		}
		if (Input.GetKeyDown((KeyCode)289) && selected != null && selected.active) 
		{
			
			
			selected.SetCategory((selected.category + 1) % 7);
			selected.naturalProgress = 0.0;
			Msg(WeatherSystem.TypeName(selected.type) + " 强度 -> " + WeatherSystem.StrengthName(selected.type, selected.category) + "  (" + selected.vmaxTarget.ToString("0") + " m/s)");
			return;
		}
		if (Input.GetKeyDown((KeyCode)290)) 
		{
			panelExpanded = !panelExpanded;
			Msg("活跃天气系统面板 " + (panelExpanded ? "展开" : "收起") + "（F9）");
			return;
		}
		
		
		
		if (Input.GetKeyDown((KeyCode)283) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) 
		{
			StormRenderer.downburstZoneShow = !StormRenderer.downburstZoneShow;
			Msg("风区黑框 " + (StormRenderer.downburstZoneShow ? "显示" : "隐藏") + "（Shift+F2）");
			return;
		}
		if (Input.GetKeyDown((KeyCode)283)) 
		{
			StormRenderer.windZoneHalf = !StormRenderer.windZoneHalf;
			Msg("风区半区 -> " + (StormRenderer.windZoneHalf ? "B(−s侧)" : "A(+s侧)") + "  (A " + StormRenderer.windZoneOffA.ToString("0.00") + " B " + StormRenderer.windZoneOffB.ToString("0.00") + " Rmax)");
			return;
		}
		if (Input.GetKeyDown((KeyCode)286)) 
		{
			StormRenderer.windZoneFine = !StormRenderer.windZoneFine;
			Msg("风区步进 -> " + (StormRenderer.windZoneFine ? "细 (±0.05)" : "粗 (±0.2)"));
			return;
		}
		if (Input.GetKeyDown((KeyCode)285)) 
		{
			float step = StormRenderer.windZoneFine ? 0.05f : 0.2f;
			if (StormRenderer.windZoneHalf)
			{
				StormRenderer.windZoneOffB = Mathf.Clamp(StormRenderer.windZoneOffB + step, -3f, 3f);
				Msg("B 半区风区 -> " + StormRenderer.windZoneOffB.ToString("0.00") + " Rmax");
			}
			else
			{
				StormRenderer.windZoneOffA = Mathf.Clamp(StormRenderer.windZoneOffA + step, -3f, 3f);
				Msg("A 半区风区 -> " + StormRenderer.windZoneOffA.ToString("0.00") + " Rmax");
			}
			return;
		}
		if (Input.GetKeyDown((KeyCode)284)) 
		{
			float step = StormRenderer.windZoneFine ? 0.05f : 0.2f;
			if (StormRenderer.windZoneHalf)
			{
				StormRenderer.windZoneOffB = Mathf.Clamp(StormRenderer.windZoneOffB - step, -3f, 3f);
				Msg("B 半区风区 -> " + StormRenderer.windZoneOffB.ToString("0.00") + " Rmax");
			}
			else
			{
				StormRenderer.windZoneOffA = Mathf.Clamp(StormRenderer.windZoneOffA - step, -3f, 3f);
				Msg("A 半区风区 -> " + StormRenderer.windZoneOffA.ToString("0.00") + " Rmax");
			}
			return;
		}
	}

	
	public WeatherSystem SpawnSystem(StormType type, Location at, double leadMeters, int catBoost, bool silent = false)
	{
		if (at == null || (Object)at.planet == (Object)null)
		{
			Msg("需要先进入一个世界（有星球）才能生成");
			return null;
		}
		if (!at.planet.HasAtmospherePhysics)
		{
			Msg(at.planet.codeName + " 没有大气，无法生成对流系统");
			return null;
		}
		if (systems.Count >= MaxSystems)
		{
			Msg("天气系统已达上限 " + MaxSystems);
			return null;
		}
		
		
		double newAngle = at.position.AngleRadians + leadMeters / at.planet.Radius;
		for (int gi = 0; gi < systems.Count; gi++)
		{
			WeatherSystem ex = systems[gi];
			if (ex == null || !ex.active || (Object)ex.planet != (Object)at.planet)
			{
				continue;
			}
			double gang = Math.Abs(WeatherSystem.WrapPi(ex.centerAngle - newAngle)) * ex.planet.Radius;
			double exZone = (ex.type == StormType.Typhoon) ? ex.Router * 0.9 : ex.Rmax * 2.4;
			if (gang < exZone)
			{
				Msg("拒绝生成：" + WeatherSystem.TypeName(type) + " 与 " + WeatherSystem.TypeName(ex.type) + " 区域重叠（禁止风暴生成在已有风暴区域）");
				return null;
			}
		}
		WeatherSystem sys = new WeatherSystem();
		double angle = newAngle;
		sys.seed = UnityEngine.Random.Range(1, 100000);
		sys.Configure(type, at.planet, angle, catBoost);
		systems.Add(sys);
		CreateRenderer(sys);
		if (!silent)
		{
			Msg(WeatherSystem.TypeName(type) + " 生成：" + WeatherSystem.StrengthName(type, sys.category) + "  半径 " + (sys.Rmax / 1000.0).ToString("0.#") + " km  云顶 " + (sys.Htop / 1000.0).ToString("0.#") + " km  峰值风 " + sys.Vmax.ToString("0") + " m/s");
		}
		if (selected == null)
		{
			selected = sys;
		}
		return sys;
	}

	private void CreateRenderer(WeatherSystem sys)
	{
		GameObject go = new GameObject("TyphoonRenderer_" + WeatherSystem.TypeName(sys.type));
		go.transform.parent = base.transform;
		StormRenderer r = go.AddComponent<StormRenderer>();
		r.storm = sys;
		r.Rebuild();
		r.spawnAnimT = 0f;   
		renderers.Add(r);
	}

	private void RemoveSystem(WeatherSystem sys)
	{
		systems.Remove(sys);
		for (int i = renderers.Count - 1; i >= 0; i--)
		{
			
			if (renderers[i] != null && renderers[i].storm == sys)
			{
				renderers[i].Clear();
				Object.Destroy(renderers[i].gameObject);
				renderers.RemoveAt(i);
			}
		}
		if (selected == sys)
		{
			selected = null;
		}
	}

	private void RemoveSystemAt(int idx)
	{
		if (idx < 0 || idx >= systems.Count)
		{
			return;
		}
		WeatherSystem sys = systems[idx];
		systems.RemoveAt(idx);
		for (int i = renderers.Count - 1; i >= 0; i--)
		{
			if (renderers[i] != null && renderers[i].storm == sys)
			{
				renderers[i].Clear();
				Object.Destroy(renderers[i].gameObject);
				renderers.RemoveAt(i);
			}
		}
		if (selected == sys)
		{
			selected = null;
		}
	}

	public static Location GetPlayerLocation()
	{
		try
		{
			PlayerController val = PlayerController.main;
			if ((Object)val == (Object)null || (Object)(object)((Obs_Destroyable<Player>)(object)val.player).Value == (Object)null)
			{
				return null;
			}
			Player value = ((Obs_Destroyable<Player>)(object)val.player).Value;
			if ((Object)value == (Object)null || (Object)(object)value.location == (Object)null)
			{
				return null;
			}
			return value.location.Value;
		}
		catch
		{
			return null;
		}
	}

	
	private void ProbePlayer()
	{
		pValid = false;
		Location playerLocation = GetPlayerLocation();
		if (playerLocation == null || (Object)playerLocation.planet == (Object)null)
		{
			return;
		}
		Double2 wind = Double2.zero;
		bool any = false;
		WeatherSystem nearest = null;
		double nearestDist = double.MaxValue;
		for (int i = 0; i < systems.Count; i++)
		{
			WeatherSystem sys = systems[i];
			if (sys == null || !sys.active || (Object)(object)sys.planet != (Object)(object)playerLocation.planet)
			{
				continue;
			}
			sys.Probe(playerLocation.position, out var s, out var h, out var u, out var w);
			if (Math.Abs(s) < nearestDist)
			{
				nearestDist = Math.Abs(s);
				nearest = sys;
				pS = s;
				pH = h;
				
				
				Double2 vecW = sys.SampleWindLocal(s, h, playerLocation.position);
				Double2 nrm2 = playerLocation.position.normalized;
				Double2 tanV = new Double2(0.0 - nrm2.y, nrm2.x);
				pU = Double2.Dot(vecW, tanV);
				pW = Double2.Dot(vecW, nrm2);
			}
			wind += sys.SampleWindLocal(s, h, playerLocation.position);
			any = true;
		}
		if (!any)
		{
			diagPValid = false;
			return;
		}
		Double2 val2 = playerLocation.velocity - wind;
		pAirspeed = val2.magnitude;
		pValid = true;
		
		diagPValid = true;
		diagWindSum = wind.magnitude;
		if (nearest != null)
		{
			diagType = WeatherSystem.TypeName(nearest.type);
			diagRo = Math.Abs(pS) / nearest.Rmax;
			diagHkm = pH / 1000.0;
			diagStrT = nearest.tornadoStrength;
			diagStrD = nearest.downburstStrength;
			diagU = pU;
			diagW = pW;
			
			
			
			diagDro = Math.Abs(pS) / Math.Max(nearest.Rmax * 0.9, 300.0);
			diagDspd = nearest.DownburstSpeedAt(pS, pH, playerLocation.position);
			
			
			if (nearest.downburstStrength > 0.05 && Time.unscaledTime - lastDLog > 2f)
			{
				lastDLog = Time.unscaledTime;
				Debug.Log("[Typhoon] 下暴诊断 类型=" + diagType + " str=" + diagStrD.ToString("0.00") + " dro=" + diagDro.ToString("0.00") + " spd=" + diagDspd.ToString("0") + "m/s u=" + diagU.ToString("0") + " w=" + diagW.ToString("0") + " h=" + diagHkm.ToString("0.00") + "km");
			}
		}
		_ = nearest;
	}

	private void ApplyShake()
	{
		if (!TyphoonConfig.I.cameraShake || !pValid)
		{
			return;
		}
		shakeCooldown -= Time.unscaledDeltaTime;
		if (shakeCooldown > 0f)
		{
			return;
		}
		shakeCooldown = 0.12f;
		double num = Math.Sqrt(pU * pU + pW * pW);
		if (num < 12.0)
		{
			return;
		}
		PlayerController val = PlayerController.main;
		if ((Object)val == (Object)null)
		{
			return;
		}
		Location playerLocation = GetPlayerLocation();
		if (playerLocation == null)
		{
			return;
		}
		
		
		float num2 = (float)(Math.Min(num / 70.0, 1.4) * 0.135 * TyphoonConfig.I.cameraShakeScale);
		num2 *= 1f + ProximityFactor() * 3f;
		try
		{
			val.CreateShakeEffect(num2, 0.18f, 100000f, WorldView.ToLocalPosition(playerLocation.position));
		}
		catch
		{
		}
	}

	
	private void OnGUI()
	{
		if (!menuOpen || !TyphoonConfig.I.hud)
		{
			return;
		}
		float bw = 680f;
		float bh = 268f;
		float baseX = ((float)Screen.width - bw) * 0.5f;
		float baseY = 70f;
		Rect w = new Rect(baseX + menuOffset.x, baseY + menuOffset.y, bw, bh);
		Event e = Event.current;
		
		if (e.type == EventType.MouseDown && e.button == 0 && new Rect(w.x, w.y, bw, 34f).Contains(e.mousePosition))
		{
			menuDragging = true;
			menuGrabDelta = e.mousePosition - new Vector2(w.x, w.y);
			e.Use();
		}
		else if (e.type == EventType.MouseDrag && menuDragging)
		{
			menuOffset = e.mousePosition - menuGrabDelta - new Vector2(baseX, baseY);
			e.Use();
		}
		else if (e.type == EventType.MouseUp && menuDragging)
		{
			menuDragging = false;
			e.Use();
		}
		
		GUIStyle box = new GUIStyle(GUI.skin.box);
		box.alignment = TextAnchor.UpperLeft;
		box.fontSize = 12;
		box.normal.background = DeepSpaceTex();
		box.border = new RectOffset(8, 8, 8, 8);
		GUI.Box(w, GUIContent.none, box);
		float x = w.x + 14f;
		float y = w.y + 10f;
		GUIStyle title = new GUIStyle(GUI.skin.label);
		title.fontSize = 17;
		title.fontStyle = FontStyle.Bold;
		title.normal.textColor = new Color(0.72f, 0.86f, 1f);
		GUI.Label(new Rect(x, y, 420f, 24f), "🌪 气象指挥中心", title);
		GUIStyle small = new GUIStyle(GUI.skin.label);
		small.fontSize = 11;
		small.normal.textColor = new Color(0.52f, 0.64f, 0.8f);
		GUI.Label(new Rect(x + 170f, y + 8f, 420f, 18f), "点击类型即刻召唤 · 拖标题栏可移动", small);
		GUIStyle btn = new GUIStyle(GUI.skin.button);
		btn.fontSize = 12;
		btn.normal.textColor = new Color(0.82f, 0.91f, 1f);
		if (GUI.Button(new Rect(w.x + w.width - 64f, y, 50f, 22f), "关闭", btn))
		{
			menuOpen = false;
			return;
		}
		y += 32f;
		
		float cw = 208f;
		float ch = 52f;
		float gapX = 10f;
		int n = WeatherSystem.Spec.Length;
		for (int i = 0; i < n; i++)
		{
			int col = i % 3;
			int row = i / 3;
			float bx = x + col * (cw + gapX);
			float by = y + row * (ch + 8f);
			bool sel = i == selectedType;
			GUIStyle tb = new GUIStyle(sel ? GUI.skin.box : GUI.skin.button);
			tb.fontSize = 13;
			tb.fontStyle = FontStyle.Bold;
			tb.alignment = TextAnchor.MiddleCenter;
			tb.normal.textColor = sel ? new Color(0.55f, 0.86f, 1f) : new Color(0.85f, 0.92f, 1f);
			if (GUI.Button(new Rect(bx, by, cw, ch), WeatherSystem.Spec[i].name + "\n" + WeatherSystem.Spec[i].desc, tb))
			{
				selectedType = i;
				Location loc = GetPlayerLocation();
				if (loc != null && loc.planet != null)
				{
					SpawnSystem((StormType)i, loc, TyphoonConfig.I.spawnLeadDistanceMeters, 0);
				}
			}
		}
		y += 2 * (ch + 8f) + 12f;
		
		GUI.Label(new Rect(x, y - 8f, bw - 28f, 2f), "", new GUIStyle(GUI.skin.label) { normal = new GUIStyleState { background = SolidLine() } });
		
		float opW = 122f;
		float opGap = 8f;
		if (GUI.Button(new Rect(x, y, opW, 24f), "🌀 加龙卷", btn))
		{
			AddPhenomenon(1);
		}
		if (GUI.Button(new Rect(x + (opW + opGap), y, opW, 24f), "💨 加下击暴流", btn))
		{
			AddPhenomenon(2);
		}
		if (GUI.Button(new Rect(x + (opW + opGap) * 2f, y, opW, 24f), "清除附属", btn))
		{
			if (selected != null && selected.active)
			{
				selected.ClearPhenomena();
				Msg("已清除 " + WeatherSystem.TypeName(selected.type) + " 的附属现象");
			}
			else
			{
				Msg("请先在底部监控面板选中一个风暴");
			}
		}
		if (GUI.Button(new Rect(x + (opW + opGap) * 3f, y, opW, 24f), "全部分散", btn))
		{
			DespawnAll();
		}
		if (GUI.Button(new Rect(x + (opW + opGap) * 4f, y, opW, 24f), "风眼对准", btn))
		{
			if (selected != null && selected.active)
			{
				Location loc = GetPlayerLocation();
				if (loc != null)
				{
					selected.centerAngle = loc.position.AngleRadians;
					Msg("风眼已对准当前位置");
				}
			}
		}
		y += 32f;
		
		string info;
		if (selected != null && selected.active)
		{
			info = "已选中  " + WeatherSystem.TypeName(selected.type) + "  " + WeatherSystem.StrengthName(selected.type, selected.category)
				+ "  [" + (selected.stage == 0 ? "发展" : (selected.stage == 1 ? "成熟" : "消散")) + "]"
				+ "  峰风 " + selected.Vmax.ToString("0") + " m/s  半径 " + (selected.Rmax / 1000.0).ToString("0.##") + " km  云顶 " + (selected.Htop / 1000.0).ToString("0.0") + " km"
				+ "   [F8] 换档 [F7] 解散";
		}
		else
		{
			info = "未选中系统 — 在底部监控面板点击风暴后，方可添加龙卷 / 下击暴流";
		}
		GUIStyle infoSt = new GUIStyle(GUI.skin.label);
		infoSt.fontSize = 12;
		infoSt.normal.textColor = new Color(0.78f, 0.88f, 1f);
		GUI.Label(new Rect(x, y, bw - 28f, 20f), info, infoSt);
	}

	
	private void AddPhenomenon(int kind)
	{
		if (selected == null || !selected.active)
		{
			Msg("请先在底部监控面板选中一个风暴（点击系统行）");
			return;
		}
		if (kind == 1)
		{
			if (selected.AddTornado())
			{
				Msg("🌀 已为 " + WeatherSystem.TypeName(selected.type) + " 添加龙卷");
			}
			else
			{
				Msg("✖ " + WeatherSystem.TypeName(selected.type) + " 无法产生龙卷（仅 超级单体 / 飑线 / MCS 可挂载）");
			}
		}
		else if (kind == 2)
		{
			
			if (selected.AddDownburst())
			{
				Msg("💨 已为 " + WeatherSystem.TypeName(selected.type) + " 添加下击暴流");
			}
			else
			{
				Msg("✖ " + WeatherSystem.TypeName(selected.type) + " 无法产生下击暴流（仅 超级单体 / 飑线 / MCS 可挂载）");
			}
		}
	}

	private static Texture2D deepSpaceTex;

	private static Texture2D DeepSpaceTex()
	{
		if (deepSpaceTex == null)
		{
			deepSpaceTex = new Texture2D(1, 1);
			deepSpaceTex.SetPixel(0, 0, new Color(0.016f, 0.024f, 0.05f, 0.94f));
			deepSpaceTex.Apply();
		}
		return deepSpaceTex;
	}

	private static Texture2D lineTex;

	private static Texture2D SolidLine()
	{
		if (lineTex == null)
		{
			lineTex = new Texture2D(1, 1);
			lineTex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.14f));
			lineTex.Apply();
		}
		return lineTex;
	}

	public void DespawnAll()
	{
		for (int i = systems.Count - 1; i >= 0; i--)
		{
			RemoveSystemAt(i);
		}
		selected = null;
		Msg("全部天气系统已消散（剩余 " + systems.Count + "）");
	}

	public static void Msg(string s)
	{
		Debug.Log((object)("[Typhoon] " + s));
		try
		{
			MsgDrawer.main.Log(s);
		}
		catch
		{
		}
	}
}
