using System;
using System.Collections.Generic;
using SFS.WorldBase;
using UnityEngine;

namespace MultiWeatherEngine;

public enum StormType
{
	Typhoon,      
	Cell,         
	Multicell,    
	Supercell,    
	SquallLine,   
	MCS           
	
	
}

public class WeatherSystem
{
	
	
	
	
	public class TypeSpec
	{
		public string name;        
		public double rmaxFrac;    
		public double htopFrac;    
		public double hbaseM;      
		public double aspectMin;   
		public double vmaxMs;      
		public double updraft;     
		public double lifetimeSec; 
		public double rotate;      
		public double downburst;   
		public double line;        
		public double gustFront;   
		public bool tornadoHost;   
		public int defaultCat;     
		public string desc;        
	}

	public static readonly TypeSpec[] Spec = new TypeSpec[6]
	{
		
		
		new TypeSpec { name = "台风",        rmaxFrac = 0.0079, htopFrac = 0.15, hbaseM = 600,  aspectMin = 0.45, vmaxMs = 62, updraft = 0.32, lifetimeSec = 0,      rotate = 1.0,  downburst = 0.0,  line = 0.0,  gustFront = 0.0,  tornadoHost = false, defaultCat = 4, desc = "热带气旋（最大天气系统），持续" },
		new TypeSpec { name = "中尺度对流系统", rmaxFrac = 0.0044, htopFrac = 0.13, hbaseM = 1200, aspectMin = 0.50, vmaxMs = 30, updraft = 0.50, lifetimeSec = 21600, rotate = 0.30, downburst = 0.30, line = 0.50, gustFront = 0.75, tornadoHost = true,  defaultCat = 4, desc = "MCS：大尺度，内含弓形飑线+多涡旋" },
		new TypeSpec { name = "超级单体",    rmaxFrac = 0.0019,  htopFrac = 0.12, hbaseM = 1000, aspectMin = 0.50, vmaxMs = 38, updraft = 0.55, lifetimeSec = 14400, rotate = 0.85, downburst = 0.30, line = 0.0,  gustFront = 0.40, tornadoHost = true,  defaultCat = 4, desc = "超级单体：深厚中气旋，可产龙卷" },
		new TypeSpec { name = "飑线",        rmaxFrac = 0.0031,  htopFrac = 0.12, hbaseM = 1200, aspectMin = 0.40, vmaxMs = 32, updraft = 0.50, lifetimeSec = 14400, rotate = 0.10, downburst = 0.40, line = 0.85, gustFront = 0.95, tornadoHost = true,  defaultCat = 3, desc = "飑线：线状+弓形+阵风锋，可产龙卷" },
		new TypeSpec { name = "多单体",      rmaxFrac = 0.0013,  htopFrac = 0.11, hbaseM = 1200, aspectMin = 0.40, vmaxMs = 25, updraft = 0.50, lifetimeSec = 10800, rotate = 0.10, downburst = 0.0, line = 0.0,  gustFront = 0.30, tornadoHost = false, defaultCat = 2, desc = "多单体风暴：团状，新生单体更替，2-6h" },
		new TypeSpec { name = "单体",        rmaxFrac = 0.0006,  htopFrac = 0.10, hbaseM = 1200, aspectMin = 0.40, vmaxMs = 18, updraft = 0.50, lifetimeSec = 2100,  rotate = 0.0,  downburst = 0.0, line = 0.0,  gustFront = 0.20, tornadoHost = false, defaultCat = 1, desc = "普通单体雷暴：25-45min 三阶段" }
	};

	public static string TypeName(StormType t)
	{
		return Spec[(int)t].name;
	}

	
	
	public static string StrengthName(StormType t, int cat)
	{
		if (t == StormType.Typhoon)
		{
			return Category.Names[Category.Clamp(cat)];
		}
		string[] g = { "微弱", "弱", "中等", "较强", "强", "很强", "极端" };
		return g[Category.Clamp(cat)];
	}

	
	
	
	public static double TyphoonEyeR(int cat)
	{
		if (cat <= 1)
		{
			return 0.0;
		}
		if (cat == 2)
		{
			return 0.22;
		}
		if (cat == 3)
		{
			return 0.30;
		}
		if (cat == 4)
		{
			return 0.36;
		}
		if (cat == 5)
		{
			return 0.38;
		}
		return 0.40;
	}

	
	public StormType type = StormType.Cell;
	public Planet planet;
	public bool active;
	public int category = 3;
	public double centerAngle;
	public double Rmax;
	public double Router;
	public double Htop;
	public double Hbase;   
	public double Vmax;
	public double Wmax;
	public double drift = 9.0;        
	public double driftAngle;         
	public double age;
	public double lifetime;           
	public double developTime;        
	public double dissolveTime;       
	public int stage;                 
	public double intensity = 1.0;    
	public int seed;
	
	
	
	public double StageProgress()
	{
		if (lifetime <= 0.0)
		{
			return 0.0;
		}
		if (age < developTime)
		{
			return Clamp01(age / Math.Max(developTime, 1.0));
		}
		if (age < lifetime - dissolveTime)
		{
			return Clamp01((age - developTime) / Math.Max(lifetime - dissolveTime - developTime, 1.0));
		}
		return Clamp01((age - (lifetime - dissolveTime)) / Math.Max(dissolveTime, 1.0));
	}

	
	public double LifeFrac()
	{
		return (lifetime > 0.0) ? Math.Min(1.0, age / lifetime) : -1.0;
	}

	
	
	
	
	public class FxInst
	{
		public double sOff;        
		public double strength;    
		public double phase;       
		public bool dissolving;
		public bool manual;        
		public double seed;        
	}
	public List<FxInst> tornadoes = new List<FxInst>();
	public List<FxInst> downbursts = new List<FxInst>();
	public double tornadoStrength;    
	public double downburstStrength;  
	
	public double phaseTornado;
	public double phaseDownburst;
	
	
	public bool dissolvingTornado;
	public bool dissolvingDownburst;
	
	
	public int tornadoLevel = 3;
	
	public double rmaxBase;
	public double vmaxBase;
	public int mergeCount;

	
	
	public double vmaxDisplay;
	public double vmaxTarget;
	public double wmaxDisplay;
	
	
	public double naturalProgress;      
	public double naturalUpTime = 300.0;
	
	
	public StormType transitionTo;
	public double transitionAnimT = -1.0;
	
	public bool RebuildPuffsFlag;

	
	
	
	
	public WeatherSystem mergeTarget;
	public double mergeAnimT = -1.0;
	public int mergeMode;
	public double mergeMidAng;

	
	
	public float MergeFade()
	{
		if (mergeAnimT >= 0.0 && mergeMode != 2)
		{
			return (float)(1.0 - Math.Min(1.0, mergeAnimT) * 0.9);
		}
		return 1f;
	}

	
	public Double2 MergedStormC()
	{
		Double2 c = StormCenterPos();
		if (mergeAnimT >= 0.0 && planet != null)
		{
			double t = Math.Min(1.0, mergeAnimT);
			if (mergeMode == 2)
			{
				Double2 midC = new Double2(Math.Cos(mergeMidAng) * planet.Radius, Math.Sin(mergeMidAng) * planet.Radius);
				c = Double2.Lerp(c, midC, t);
			}
			else if (mergeTarget != null && mergeTarget.planet != null)
			{
				Double2 tc = new Double2(Math.Cos(mergeTarget.centerAngle) * planet.Radius, Math.Sin(mergeTarget.centerAngle) * planet.Radius);
				c = Double2.Lerp(c, tc, t);
			}
		}
		return c;
	}

	
	
	
	
	
	
	
	public static int WindZoneIndex(double sR)
	{
		if (sR <= -3.0) return 0;
		if (sR <= -2.2) return 1;
		if (sR <= -1.6) return 2;
		if (sR <= -1.2) return 3;
		if (sR <= -0.7) return 4;
		if (sR <= 0.7) return 5;
		if (sR <= 1.2) return 6;
		if (sR <= 1.6) return 7;
		if (sR <= 2.2) return 8;
		if (sR <= 3.0) return 9;
		return 10;
	}

	
	
	public static readonly double[] beaufortMin = { 0.0, 0.3, 1.6, 3.4, 5.5, 8.0, 10.8, 13.9, 17.2, 20.8, 24.5, 28.5, 32.7, 37.0, 41.5, 46.2, 51.0, 56.1, 61.2 };

	
	public static int Beaufort(double ms)
	{
		int lv = 0;
		for (int i = 0; i < beaufortMin.Length; i++)
		{
			if (ms >= beaufortMin[i])
			{
				lv = i;
			}
		}
		return lv;
	}

	
	
	private static double BaseProfile(double aR)
	{
		if (aR <= 0.5)
		{
			return 0.2 + 0.3 * SmoothStep(aR, 0.0, 0.5);
		}
		if (aR <= 1.0)
		{
			return 0.5 + 0.5 * SmoothStep(aR, 0.5, 1.0);
		}
		return 1.0 - 0.8 * SmoothStep(aR - 1.0, 0.0, 1.5);
	}

	
	
	
	
	
	
	
	public double WindCircleRo(double levelMin, bool baseOnly = false)
	{
		double want = levelMin / Math.Max(vmaxDisplay * intensity, 1.0);
		double maxA = Router / Rmax;
		for (double a = maxA; a > 0.0; a -= 0.1)
		{
			double p = BaseProfile(a) * Math.Exp(0.0 - Pow2(a / 2.5));
			if (baseOnly ? (p >= want) : (p * StormRenderer.windZoneGain[WindZoneIndex(a)] >= want))
			{
				return a;
			}
		}
		return 0.0;
	}

	
	
	
	public bool manualTornado;
	public bool manualDownburst;

	
	
	private void RandomPos(FxInst fx, double sMax, int attempt = 0)
	{
		ulong n = (ulong)(tornadoes.Count + downbursts.Count + 1) + (ulong)attempt * 7919u;
		double h1 = (double)(((ulong)seed * 2654435761u + n * 97u) % 10000) / 10000.0;
		double h2 = (double)(((ulong)seed * 2246822519u + n * 131u) % 10000) / 10000.0;
		fx.sOff = (h1 * 2.0 - 1.0) * (0.2 + sMax * h2);   
		fx.seed = (double)(((ulong)seed * 40503u + n * 911u) % 10000) / 100.0;
	}

	
	private bool PosTooClose(double sOff, double minGap, double crossGap)
	{
		foreach (FxInst o in tornadoes)
		{
			if (Math.Abs(o.sOff - sOff) * Rmax < minGap * Rmax)
			{
				return true;
			}
		}
		foreach (FxInst o in downbursts)
		{
			if (Math.Abs(o.sOff - sOff) * Rmax < crossGap * Rmax)
			{
				return true;
			}
		}
		return false;
	}

	
	private void RandomPosAvoid(FxInst fx, double sMax, bool isTornado)
	{
		double minGap = isTornado ? 0.6 : 0.5;   
		double crossGap = isTornado ? 0.4 : 0.35; 
		for (int k = 0; k < 12; k++)
		{
			RandomPos(fx, sMax, k);
			if (!PosTooClose(fx.sOff, minGap, crossGap))
			{
				return;
			}
		}
	}

	public bool AddTornado()
	{
		if (Spec[(int)type].tornadoHost)
		{
			FxInst fx = new FxInst();
			
			
			fx.strength = 0.5 + category / 6.0;
			fx.phase = 0.0;                     
			fx.dissolving = false;
			fx.manual = true;                   
			RandomPosAvoid(fx, 0.9, true);      
			tornadoes.Add(fx);
			tornadoStrength = fx.strength;      
			phaseTornado = fx.phase;
			dissolvingTornado = false;
			manualTornado = true;
			return true;
		}
		return false;
	}

	
	
	public bool CanDownburst()
	{
		return type == StormType.Supercell || type == StormType.SquallLine || type == StormType.MCS;
	}

	
	public bool AddDownburst()
	{
		if (!CanDownburst())
		{
			return false;
		}
		FxInst fx = new FxInst();
		
		fx.strength = 0.5 + category / 6.0;
		fx.phase = 0.0;
		fx.dissolving = false;
		fx.manual = true;
		RandomPosAvoid(fx, 0.6, false);         
		downbursts.Add(fx);
		downburstStrength = fx.strength;        
		phaseDownburst = fx.phase;
		dissolvingDownburst = false;
		manualDownburst = true;
		return true;
	}

	
	
	public void ClearPhenomena()
	{
		foreach (FxInst fx in tornadoes)
		{
			fx.dissolving = fx.strength > 0.001;
		}
		foreach (FxInst fx in downbursts)
		{
			fx.dissolving = fx.strength > 0.001;
		}
		dissolvingTornado = tornadoes.Count > 0;
		dissolvingDownburst = downbursts.Count > 0;
	}

	
	
	
	private void MergeFx()
	{
		if (tornadoes.Count > 1)
		{
			for (int i = 0; i < tornadoes.Count; i++)
			{
				for (int j = i + 1; j < tornadoes.Count; j++)
				{
					FxInst a = tornadoes[i];
					FxInst b = tornadoes[j];
					if (Math.Abs(a.sOff - b.sOff) * Rmax < 0.4 * Rmax)
					{
						double wSum = a.strength + b.strength;
						double mid = (a.sOff * a.strength + b.sOff * b.strength) / wSum;
						if (a.strength >= b.strength)
						{
							a.sOff = mid;
							a.strength = Math.Min(2.0, a.strength + b.strength * 0.25);   
							tornadoes.RemoveAt(j);
						}
						else
						{
							b.sOff = mid;
							b.strength = Math.Min(2.0, b.strength + a.strength * 0.25);
							tornadoes.RemoveAt(i);
						}
						return;
					}
				}
			}
		}
		if (downbursts.Count > 1)
		{
			for (int i = 0; i < downbursts.Count; i++)
			{
				for (int j = i + 1; j < downbursts.Count; j++)
				{
					FxInst a = downbursts[i];
					FxInst b = downbursts[j];
					if (Math.Abs(a.sOff - b.sOff) * Rmax < 0.4 * Rmax)
					{
						double wSum = a.strength + b.strength;
						double mid = (a.sOff * a.strength + b.sOff * b.strength) / wSum;
						if (a.strength >= b.strength)
						{
							a.sOff = mid;
							a.strength = Math.Min(2.0, a.strength + b.strength * 0.25);
							downbursts.RemoveAt(j);
						}
						else
						{
							b.sOff = mid;
							b.strength = Math.Min(2.0, b.strength + a.strength * 0.25);
							downbursts.RemoveAt(i);
						}
						return;
					}
				}
			}
		}
	}

	public void Configure(StormType t, Planet p, double angle, int catBoost)
	{
		TypeSpec spec = Spec[(int)t];
		type = t;
		planet = p;
		centerAngle = angle;
		category = Category.Clamp(spec.defaultCat + catBoost);
		
		
		
		Rmax = 6371000.0 * spec.rmaxFrac * 0.3;
		
		
		double atmTop = 60000.0;
		if (p.HasAtmospherePhysics && p.AtmosphereHeightPhysics > 1000.0)
		{
			atmTop = p.AtmosphereHeightPhysics;
		}
		
		Htop = 100000.0 * spec.htopFrac * 0.3;
		if (Htop < 500.0)
		{
			Htop = 500.0;
		}
		if (Htop > atmTop * 0.95)
		{
			Htop = atmTop * 0.95;
		}
		
		
		Hbase = spec.hbaseM * 0.3;
		if (Hbase < 30.0)
		{
			Hbase = 30.0;
		}
		if (Hbase > Htop * 0.4)
		{
			Hbase = Htop * 0.4;
		}
		double minR = Htop * spec.aspectMin;
		if (Rmax < minR)
		{
			Rmax = minR;
		}
		if (Rmax < 100.0)
		{
			Rmax = 100.0;
		}
		double maxR = p.Radius * 0.3;
		if (Rmax > maxR)
		{
			Rmax = maxR;
		}
		Router = Rmax * 9.0;
		
		
		
		
		
		double catF = (type == StormType.Typhoon) ? (0.2 + category / 6.0 * 0.9) : (0.5 + category / 6.0);
		Vmax = spec.vmaxMs * catF;
		Wmax = Vmax * spec.updraft;
		rmaxBase = Rmax;
		vmaxBase = Vmax;
		mergeCount = 0;
		tornadoStrength = 0.0;
		downburstStrength = 0.0;
		phaseTornado = 0.0;
		phaseDownburst = 0.0;
		if (spec.lifetimeSec > 0.0)
		{
			lifetime = spec.lifetimeSec * (0.8 + 0.4 * (double)seed * 0.5 + 0.4 * 0.5);
		}
		else
		{
			lifetime = 0.0;
		}
		age = 0.0;
		stage = 0;
		intensity = 0.4;
		
		
		developTime = 60.0 + (double)(seed % 61);
		dissolveTime = (lifetime > 0.0) ? Math.Max(30.0, lifetime * 0.1) : 0.0;
		
		
		
		drift = (5.0 + spec.vmaxMs * 0.15 + 6.0 * ((double)(seed % 97) / 97.0)) * 0.35;
		driftAngle = (double)(seed % 6283) / 1000.0;
		active = true;
		
		vmaxDisplay = Vmax;
		vmaxTarget = Vmax;
		wmaxDisplay = Wmax;
		naturalProgress = 0.0;
	}

	
	
	public void SetCategory(int cat)
	{
		category = Category.Clamp(cat);
		double catF = (type == StormType.Typhoon) ? (0.2 + category / 6.0 * 0.9) : (0.5 + category / 6.0);
		vmaxTarget = Spec[(int)type].vmaxMs * catF;
		SyncFxStrength();
	}

	
	private void SyncFxStrength()
	{
		double s = 0.5 + category / 6.0;
		foreach (FxInst fx in tornadoes)
		{
			fx.strength = s;
		}
		foreach (FxInst fx in downbursts)
		{
			fx.strength = s;
		}
		tornadoStrength = (tornadoes.Count > 0) ? s : 0.0;
		downburstStrength = (downbursts.Count > 0) ? s : 0.0;
	}

	
	public void TransitionTo(StormType newType)
	{
		if (newType == type || transitionAnimT >= 0.0 || newType == StormType.Typhoon)
		{
			return;
		}
		transitionTo = newType;
		transitionAnimT = 0.0;
	}

	
	public double TransitionBlend()
	{
		return (transitionAnimT >= 0.0) ? Math.Min(1.0, transitionAnimT) : 0.0;
	}

	
	private void ApplyTypeScale()
	{
		TypeSpec spec = Spec[(int)type];
		Rmax = 6371000.0 * spec.rmaxFrac * 0.3;
		double atmTop = 60000.0;
		if (planet != null && planet.HasAtmospherePhysics && planet.AtmosphereHeightPhysics > 1000.0)
		{
			atmTop = planet.AtmosphereHeightPhysics;
		}
		Htop = 100000.0 * spec.htopFrac * 0.3;
		if (Htop < 500.0)
		{
			Htop = 500.0;
		}
		if (Htop > atmTop * 0.95)
		{
			Htop = atmTop * 0.95;
		}
		Hbase = spec.hbaseM * 0.3;
		if (Hbase < 30.0)
		{
			Hbase = 30.0;
		}
		if (Hbase > Htop * 0.4)
		{
			Hbase = Htop * 0.4;
		}
		double minR = Htop * spec.aspectMin;
		if (Rmax < minR)
		{
			Rmax = minR;
		}
		if (Rmax < 100.0)
		{
			Rmax = 100.0;
		}
		double maxR = (planet != null) ? planet.Radius * 0.3 : 3.0e6;
		if (Rmax > maxR)
		{
			Rmax = maxR;
		}
		Router = Rmax * 9.0;
		rmaxBase = Rmax;
		vmaxBase = Vmax;
		SetCategory(category);
	}

	public void Advance(double dt)
	{
		if (!active || planet == null)
		{
			return;
		}
		if (dt < 0.0)
		{
			dt = 0.0;
		}
		if (dt > 5.0)
		{
			dt = 5.0;
		}
		
		
		MergeFx();
		age += dt;
		
		if (vmaxDisplay != vmaxTarget)
		{
			double k = Clamp01(dt / 3.0);
			vmaxDisplay += (vmaxTarget - vmaxDisplay) * k;
			if (Math.Abs(vmaxTarget - vmaxDisplay) < 0.5)
			{
				vmaxDisplay = vmaxTarget;
			}
			TypeSpec sp = Spec[(int)type];
			wmaxDisplay = vmaxDisplay * sp.updraft;
		}
		
		if (transitionAnimT >= 0.0)
		{
			transitionAnimT += dt / 3.0;
			if (transitionAnimT >= 1.0)
			{
				type = transitionTo;
				transitionAnimT = -1.0;
				ApplyTypeScale();
				RebuildPuffsFlag = true;   
			}
		}
		
		if (mergeAnimT >= 0.0)
		{
			mergeAnimT += dt / 3.0;
		}
		if (lifetime > 0.0)
		{
			
			
			if (age < developTime)
			{
				stage = 0;
				intensity = 0.4 + 0.6 * (age / developTime);
			}
			else if (age < lifetime - dissolveTime)
			{
				stage = 1;
				intensity = 1.0;
			}
			else
			{
				stage = 2;
				intensity = 1.0 - 0.8 * Clamp01((age - (lifetime - dissolveTime)) / Math.Max(dissolveTime, 1.0));
			}
			if (age >= lifetime)
			{
				active = false;
				return;
			}
		}
		else
		{
			intensity = 1.0;
		}
		
		driftAngle += (0.05 + 0.2 * (double)(seed % 13) / 13.0) * dt * 0.02;
		centerAngle = WrapTwoPi(centerAngle + drift / planet.Radius * dt);

		
		
		TypeSpec spec = Spec[(int)type];
		if (stage == 1)
		{
			
			
			
			naturalProgress += dt / naturalUpTime;
			if (naturalProgress >= 1.0 && category < 6)
			{
				naturalProgress = 0.0;
				SetCategory(category + 1);
			}
			
			
			
			if (transitionAnimT < 0.0 && type != StormType.Typhoon && (ulong)(seed * 2246822519u) % 10000 < dt * 2.0)
			{
				StormType nt = StormType.Cell;
				if (type == StormType.Cell)
				{
					nt = StormType.Multicell;
				}
				else if (type == StormType.Multicell)
				{
					nt = (UnityEngine.Random.value < 0.5) ? StormType.Supercell : StormType.MCS;
				}
				else if (type == StormType.Supercell)
				{
					nt = StormType.MCS;
				}
				else if (type == StormType.SquallLine)
				{
					nt = StormType.MCS;
				}
				if (nt != type)
				{
					TransitionTo(nt);
				}
			}
			if (spec.tornadoHost)
			{
				
				
				for (int ti = tornadoes.Count - 1; ti >= 0; ti--)
				{
					FxInst fx = tornadoes[ti];
					if (fx.dissolving || !fx.manual)
					{
						fx.strength -= dt / (fx.dissolving ? 2.5 : 150.0);
					}
					if (fx.strength <= 0.0)
					{
						tornadoes.RemoveAt(ti);
					}
					else
					{
						fx.phase = Math.Min(1.0, fx.phase + dt / 3.0);
					}
				}
				if (tornadoes.Count < 4 && ((double)(seed % 17) == 0.0 || (seed * 2654435761u % 10000) < dt * 60.0))
				{
					FxInst fx = new FxInst();
					fx.strength = 0.5 + category / 6.0;   
					fx.phase = 0.0;
					fx.dissolving = false;
					fx.manual = false;                  
					RandomPosAvoid(fx, 0.9, true);   
					tornadoes.Add(fx);
				}
				tornadoStrength = 0.0;
				foreach (FxInst fx in tornadoes)
				{
					tornadoStrength = Math.Max(tornadoStrength, fx.strength);
				}
				phaseTornado = (tornadoes.Count > 0) ? tornadoes[0].phase : 0.0;
				dissolvingTornado = tornadoes.Count > 0;
			}
			
			for (int di = downbursts.Count - 1; di >= 0; di--)
			{
				FxInst fx = downbursts[di];
				if (fx.dissolving || !fx.manual)
				{
					fx.strength -= dt / (fx.dissolving ? 2.5 : 240.0);
				}
				if (fx.strength <= 0.0)
				{
					downbursts.RemoveAt(di);
				}
				else
				{
					fx.phase = Math.Min(1.0, fx.phase + dt / 3.0);
				}
			}
			if (downbursts.Count < 4 && CanDownburst() && (seed * 2654435761u % 10000) < dt * 30.0)
			{
				FxInst fx = new FxInst();
				fx.strength = 0.5 + category / 6.0;   
				fx.phase = 0.0;
				fx.dissolving = false;
				fx.manual = false;
				RandomPosAvoid(fx, 0.6, false);   
				downbursts.Add(fx);
			}
			downburstStrength = 0.0;
			foreach (FxInst fx in downbursts)
			{
				downburstStrength = Math.Max(downburstStrength, fx.strength);
			}
			phaseDownburst = (downbursts.Count > 0) ? downbursts[0].phase : 0.0;
			dissolvingDownburst = downbursts.Count > 0;
		}
		else if (stage != 1)
		{
			
			for (int ti = tornadoes.Count - 1; ti >= 0; ti--)
			{
				FxInst fx = tornadoes[ti];
				if (fx.dissolving || !fx.manual)
				{
					fx.strength = Math.Max(0.0, fx.strength - dt / 60.0);
				}
				if (fx.strength <= 0.0)
				{
					tornadoes.RemoveAt(ti);
				}
			}
			for (int di = downbursts.Count - 1; di >= 0; di--)
			{
				FxInst fx = downbursts[di];
				if (fx.dissolving || !fx.manual)
				{
					fx.strength = Math.Max(0.0, fx.strength - dt / 60.0);
				}
				if (fx.strength <= 0.0)
				{
					downbursts.RemoveAt(di);
				}
			}
			tornadoStrength = 0.0;
			foreach (FxInst fx in tornadoes)
			{
				tornadoStrength = Math.Max(tornadoStrength, fx.strength);
			}
			downburstStrength = 0.0;
			foreach (FxInst fx in downbursts)
			{
				downburstStrength = Math.Max(downburstStrength, fx.strength);
			}
		}
	}

	
	public void ToStormFrame(Double2 globalPos, out double s, out double h)
	{
		double radius = planet.Radius;
		Double2 val = globalPos;
		h = val.magnitude - radius;
		val = globalPos;
		s = WrapPi(val.AngleRadians - centerAngle) * radius;
	}

	public Double2 SampleWind(Double2 globalPos)
	{
		if (!active || planet == null)
		{
			return Double2.zero;
		}
		ToStormFrame(globalPos, out var s, out var h);
		return SampleWindLocal(s, h, globalPos);
	}

	public Double2 SampleWindLocal(double s, double h, Double2 globalPos)
	{
		SampleComponents(s, h, out var u, out var w);
		Double2 val = globalPos;
		Double2 normalized = val.normalized;
		Double2 wind = new Double2(0.0 - normalized.y, normalized.x) * u + normalized * w;
		
		
		
		if (active && planet != null)
		{
			if (tornadoStrength > 0.05)
			{
				wind += TornadoHorizontal(s, h, globalPos);
			}
			if (downburstStrength > 0.05)
			{
				wind += DownburstHorizontal(s, h, globalPos);
			}
		}
		return wind;
	}

	
	private Double2 StormCenterPos()
	{
		double R = (planet != null) ? planet.Radius : 1.0;
		return new Double2(Math.Cos(centerAngle) * R, Math.Sin(centerAngle) * R);
	}

	
	private Double2 FxCenterPos(FxInst fx)
	{
		double R = (planet != null) ? planet.Radius : 1.0;
		Double2 c = new Double2(Math.Cos(centerAngle) * R, Math.Sin(centerAngle) * R);
		Double2 tan = new Double2(0.0 - Math.Sin(centerAngle), Math.Cos(centerAngle));
		return c + tan * (fx.sOff * Rmax);
	}

	
	
	
	private Double2 TornadoHorizontal(double s, double h, Double2 globalPos)
	{
		Double2 wind = Double2.zero;
		Double2 nrm = globalPos.normalized;              
		for (int ti = 0; ti < tornadoes.Count; ti++)
		{
			FxInst fx = tornadoes[ti];
			if (fx.strength <= 0.05)
			{
				continue;
			}
			Double2 center = FxCenterPos(fx);
			Double2 r = globalPos - center;
			double rn = r.x * nrm.x + r.y * nrm.y;           
			Double2 rHoriz = r - nrm * rn;                   
			double rm = rHoriz.magnitude;
			if (rm < 0.5)
			{
				continue;
			}
			Double2 inward = new Double2(0.0 - rHoriz.x, 0.0 - rHoriz.y) / rm;   
			double sRel = s - fx.sOff * Rmax;                
			double num = Math.Abs(sRel);
			double tro = num / Math.Max(Rmax * 0.3, 40.0);
			double troZ = tro / StormRenderer.tornadoZoneHoriz;
			double rotCore = 1.0 / Math.Max(troZ, 0.55);
			double funnel = Math.Pow(1.0 - Clamp01(Math.Max(0.0, h) / Htop / 0.9), 0.7);
			double edge = Math.Exp(0.0 - Pow2(num / (Rmax * 2.5)));   
			double grow = Math.Min(fx.phase, fx.strength);
			double speed = 90.0 * fx.strength * rotCore * funnel * edge * grow;
			wind += inward * speed;
		}
		return wind;
	}

	
	
	private Double2 DownburstHorizontal(double s, double h, Double2 globalPos)
	{
		Double2 wind = Double2.zero;
		Double2 nrm = globalPos.normalized;
		for (int di = 0; di < downbursts.Count; di++)
		{
			FxInst fx = downbursts[di];
			if (fx.strength <= 0.05)
			{
				continue;
			}
			Double2 center = FxCenterPos(fx);
			Double2 r = globalPos - center;
			double rn = r.x * nrm.x + r.y * nrm.y;
			Double2 rHoriz = r - nrm * rn;
			double rm = rHoriz.magnitude;
			if (rm < 0.5)
			{
				continue;
			}
			Double2 outDir = rHoriz / rm;   
			double sRel = s - fx.sOff * Rmax;
			double num = Math.Abs(sRel);
			double dro = num / Math.Max(Rmax * 0.9, 300.0);
			double droZ = dro / StormRenderer.downburstZoneHoriz;
			double spread = 0.5 + 0.5 * SmoothStep(droZ, 0.0, 0.85);
			if (droZ > 0.85)
			{
				spread *= 1.0 - SmoothStep((droZ - 0.85) / 0.75, 0.0, 1.0);
			}
			double ground = Clamp01(1.0 - Math.Max(0.0, h) / Htop / 0.5);
			double edge = Math.Exp(0.0 - Pow2(num / (Rmax * 2.5)));   
			double grow = Math.Min(fx.phase, fx.strength);
			double speed = 60.0 * fx.strength * spread * (0.5 + 0.5 * ground) * edge * grow;
			wind += outDir * speed;
		}
		return wind;
	}

	
	public double DownburstSpeedAt(double s, double h, Double2 globalPos)
	{
		if (downburstStrength < 0.05)
		{
			return 0.0;
		}
		return DownburstHorizontal(s, h, globalPos).magnitude;
	}

	
	public void SampleComponents(double s, double h, out double u, out double w)
	{
		u = 0.0;
		w = 0.0;
		if (!active || planet == null)
		{
			return;
		}
		double num = Math.Abs(s);
		
		
		
		
		if (num > Rmax * 6.0 || h > Htop * 1.4 || h < -600.0)
		{
			return;
		}
		TypeSpec spec = Spec[(int)type];
		
		
		
		
		double sWind = s - (s >= 0.0 ? (double)StormRenderer.windZoneOffA : (double)StormRenderer.windZoneOffB) * Rmax;
		double numW = Math.Abs(sWind);
		double ro = numW / Rmax;
		double ht = Clamp01(Math.Max(0.0, h) / Htop);
		double num4 = (s >= 0.0) ? 1.0 : (-1.0);
		double edge = Math.Exp(0.0 - Pow2(numW / (Rmax * 2.5)));   
		double hDecay = Math.Exp((0.0 - ht) / 0.14);
		double gust = spec.gustFront;

		
		
		
		
		
		
		
		for (int di = 0; di < downbursts.Count; di++)
		{
			FxInst fx = downbursts[di];
			if (fx.strength <= 0.05)
			{
				continue;
			}
			double sRelD = num - Math.Abs(fx.sOff * Rmax);   
			double dro = sRelD / Math.Max(Rmax * 0.9, 300.0);
			double droV = dro / StormRenderer.downburstZoneVert;
			double sink = Math.Exp(0.0 - Pow2(droV / 1.3));
			double growD = Math.Min(fx.phase, fx.strength);
			double torYield = 1.0;
			for (int ti = 0; ti < tornadoes.Count; ti++)
			{
				FxInst tfx = tornadoes[ti];
				if (tfx.strength <= 0.05)
				{
					continue;
				}
				double troY = num / Math.Max(Rmax * 0.3, 40.0);
				torYield = 1.0 - tfx.strength * Math.Exp(0.0 - Pow2(troY / 0.7));
			}
			w -= 55.0 * fx.strength * sink * Math.Exp((0.0 - ht) / 0.35) * growD * torYield;
		}

		
		
		for (int ti = 0; ti < tornadoes.Count; ti++)
		{
			FxInst fx = tornadoes[ti];
			if (fx.strength <= 0.05)
			{
				continue;
			}
			double sRelT = num - Math.Abs(fx.sOff * Rmax);
			double tro = sRelT / Math.Max(Rmax * 0.3, 40.0);
			double troV = tro / StormRenderer.tornadoZoneVert;
			double upCore = Math.Exp(0.0 - Pow2(troV / 0.7));
			double funnel = Math.Pow(1.0 - Clamp01(ht / 0.9), 0.7);
			double growT = Math.Min(fx.phase, fx.strength);
			w += 130.0 * fx.strength * 0.42 * upCore * funnel * growT;
		}

		
		
		
		
		double torMask = 1.0;
		for (int ti = 0; ti < tornadoes.Count; ti++)
		{
			FxInst fx = tornadoes[ti];
			if (fx.strength <= 0.05)
			{
				continue;
			}
			double troM = num / Math.Max(Rmax * 0.3, 40.0);
			torMask = Math.Min(torMask, 1.0 - fx.strength * Math.Exp(0.0 - Pow2(troM / 0.8)));
		}
		if (torMask < 0.1)
		{
			torMask = 0.1;
		}

		
		
		
		
		
		
		double sR = sWind / Rmax;   
		double aR = Math.Abs(sR);
		
		
		
		double num6 = BaseProfile(aR);
		num6 *= StormRenderer.windZoneGain[WindZoneIndex(sR)];
		
		
		
		
		
		double eyeRT = (type == StormType.Typhoon) ? TyphoonEyeR(category) : 0.0;
		if (eyeRT > 0.05)
		{
			double aRN = aR / eyeRT;
			if (aRN <= 1.0)
			{
				num6 = 0.06 + 0.94 * SmoothStep(aRN, 0.0, 1.0);   
			}
			else if (aRN <= 1.6)
			{
				num6 = 1.0 - 0.5 * SmoothStep(aRN - 1.0, 0.0, 0.6);   
			}
			else
			{
				num6 = 0.5 * (1.0 - 0.8 * SmoothStep((aRN - 1.6) / 4.0, 0.0, 1.0));   
			}
			num6 *= StormRenderer.windZoneGain[WindZoneIndex(sR)];
		}
		double circle7 = WindCircleRo(13.9);
		if (circle7 > 0.25 && aR > circle7)
		{
			double want7 = 13.9 / Math.Max(vmaxDisplay * intensity, 1.0);
			double tLin = Clamp01((aR - circle7) / Math.Max(Router / Rmax - circle7, 0.01));
			num6 = want7 * (1.0 - tLin);
		}
		
		
		
		
		
		
		
		
		if (aR > Math.Max(eyeRT, 0.05))
		{
			double c7b = WindCircleRo(13.9, true);
			double c10 = WindCircleRo(24.5, true);
			double c12 = WindCircleRo(32.7, true);
			if (c7b > 0.25)
			{
				double floor = 0.0;
				if (c12 > 0.25 && aR <= c12)
				{
					floor = 32.7;
				}
				else if (c10 > 0.25 && aR <= c10)
				{
					floor = 24.5;
				}
				else if (aR <= c7b)
				{
					floor = 13.9;
				}
				else
				{
					floor = 13.9 * (1.0 - Clamp01((aR - c7b) / Math.Max(Router / Rmax - c7b, 0.01)));
				}
				floor = Math.Min(floor, vmaxDisplay * intensity * 0.9);
				double need = floor / Math.Max(vmaxDisplay * intensity, 1.0) / Math.Max(edge, 0.03);
				num6 = Math.Max(num6, need);
			}
		}
		double outflow = Math.Exp(0.0 - Pow2((ht - 0.88) / 0.14));
		double uBase = num6 * edge * ((0.0 - num4) * hDecay + num4 * TyphoonConfig.I.outflowFraction * outflow);

		
		if (spec.line > 0.05)
		{
			
			double gfr = 1.0 / Math.Max(spec.gustFront, 0.2);
			double front = Math.Exp(0.0 - Pow2((ro - gfr) / 0.35));
			uBase = num6 * edge * (0.4 + 0.9 * front) * (0.0 - num4);
		}

		
		if (spec.rotate > 0.05)
		{
			
			
			
			
			double rot = 0.4 + 0.6 * num6;
			u += vmaxDisplay * spec.rotate * 0.35 * rot * edge * hDecay * num4 * intensity * torMask;
		}

		u += vmaxDisplay * uBase * intensity * torMask;

		
		
		u += drift * edge * (0.35 + 0.65 * Math.Exp(0.0 - Pow2((ro - 1.0) / 1.2))) * hDecay;
		
		
		
		
		
		
		
		
		if (circle7 > 0.25 && aR > circle7 && aR * Rmax < Rmax * 2.5 - 100.0)
		{
			double ph = (age * 0.33 + num * 0.011 + (double)(seed % 7) * 0.13) % 1.0;
			double pulse = Math.Exp(0.0 - Pow2((ph - 0.12) / 0.09));
			double burst = 8.0 + 4.0 * ((double)((seed * 31 + (int)(num * 3.7)) % 100) / 100.0);
			u += burst * pulse * edge * num4 * (0.5 + 0.5 * hDecay);
		}
		
		
		
		double gustAmplitude = TyphoonConfig.I.gustAmplitude;
		if (gustAmplitude > 0.0001 && aR * Rmax < Rmax * 2.5 - 100.0)
		{
			float num11 = (float)(s / 3500.0 + age * 0.3);
			float num12 = (float)(h / 1200.0);
			double num13 = (double)Mathf.PerlinNoise(num11, num12) * 2.0 - 1.0;
			double num14 = (double)Mathf.PerlinNoise(num11 + 37.13f, num12 + 11.71f) * 2.0 - 1.0;
			double num15 = (double)Mathf.PerlinNoise(num11 * 3.7f + 5.5f, num12 * 3.1f) * 2.0 - 1.0;
			double num16 = edge * (0.35 + 0.65 * hDecay);
			
			
			
			double turb = gustAmplitude * (0.45 + 0.5 * (num13 * 0.8 + num15 * 0.3));
			u += vmaxDisplay * turb * num6 * num16;
			w += wmaxDisplay * gustAmplitude * (num14 * 0.9 + num15 * 0.25) * (0.3 + 0.7 * Math.Pow(Math.Sin(Math.PI * ht), 0.7)) * edge;
		}

		
		
		
		
		
		
		
		
		
		
		
		
		double vertShape;
		if (type == StormType.Typhoon)
		{
			vertShape = Math.Exp(0.0 - Pow2((ro - 1.0) / 0.42)) - 0.55 * Math.Exp(0.0 - Pow2(ro / 0.55)) - 0.22 * Math.Exp(0.0 - Pow2((ro - 2.8) / 1.2));
			
			
			
			
			vertShape *= 0.35 + 0.65 * num6;
		}
		else
		{
			vertShape = Math.Exp(0.0 - Pow2(ro / 0.9)) - 0.2 * Math.Exp(0.0 - Pow2((ro - 2.2) / 1.0));
		}
		double vShape = Math.Pow(Math.Sin(Math.PI * ht), 0.7);
		double genW = wmaxDisplay * vertShape * edge * vShape * intensity;
		if (h < 40.0)
		{
			genW *= Clamp01(h / 40.0);   
		}
		w += genW;

		
		if (gust > 0.05 && stage >= 1)
		{
			double gf = Math.Exp(0.0 - Pow2((ro - 1.2) / 0.4)) * Clamp01(1.0 - ht / 0.4);
			u += vmaxDisplay * gust * 0.5 * gf * (0.0 - num4) * intensity;
		}

		
		
		if (h > Htop)
		{
			double top = 1.0 - Clamp01((h - Htop) / (Htop * 0.4));
			u *= top;
			w *= top;
		}
	}

	private static double SmoothStep(double x, double a, double b)
	{
		double t = Clamp01((x - a) / (b - a));
		return t * t * (3.0 - 2.0 * t);
	}

	public void Probe(Double2 globalPos, out double s, out double h, out double u, out double w)
	{
		ToStormFrame(globalPos, out s, out h);
		SampleComponents(s, h, out u, out w);
	}

	
	public static double Pow2(double a)
	{
		return a * a;
	}

	public static double Clamp01(double a)
	{
		if (a < 0.0)
		{
			return 0.0;
		}
		if (a > 1.0)
		{
			return 1.0;
		}
		return a;
	}

	public static double Clamp(double a, double lo, double hi)
	{
		if (a < lo)
		{
			return lo;
		}
		if (a > hi)
		{
			return hi;
		}
		return a;
	}

	public static double WrapPi(double a)
	{
		a %= Math.PI * 2.0;
		if (a > Math.PI)
		{
			a -= Math.PI * 2.0;
		}
		if (a < -Math.PI)
		{
			a += Math.PI * 2.0;
		}
		return a;
	}

	public static double WrapTwoPi(double a)
	{
		a %= Math.PI * 2.0;
		if (a < 0.0)
		{
			a += Math.PI * 2.0;
		}
		return a;
	}
}
