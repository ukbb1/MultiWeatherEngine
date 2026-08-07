using System;
using Random = UnityEngine.Random;
using SFS.Variables;
using SFS.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace MultiWeatherEngine;

public class StormRenderer : MonoBehaviour
{
	
	
	
	
	private class RenderSet
	{
		public GameObject backGO;
		public GameObject frontGO;
		public GameObject skyGO;
		public Mesh backMesh;
		public Mesh frontMesh;
		public Mesh skyMesh;
		public Vector3[] bV;
		public Vector3[] fV;
		public Vector3[] sV;
		public Vector2[] bT;
		public Vector2[] fT;
		public Vector2[] sT;
		public Color[] bC;
		public Color[] fC;
		public Color[] sC;
		public int[] bI;
		public int[] fI;
		public int[] sI;
		public int backQuads;
		public int frontQuads;
		public bool isScaled;
		
		public MeshRenderer backR;
		public MeshRenderer frontR;
		public MeshRenderer skyR;
		public int backOrder;
		public int frontOrder;
		public int skyOrder;
	}

	private struct Puff
	{
		public Double2 pos;
		public float size;
		public float baseAlpha;
		public float life;
		public float maxLife;
		public float seed;
		public int kind;
	}

	private struct Drop
	{
		public Vector2 local;
		public Vector2 vel;
		public float len;
		public float alpha;
	}

	private const int SortBack = 20;
	private const int SortFront = 210;
	private const double VisualOuterRho = 4.6;
	private const int SkyNX = 48;
	private const int SkyNY = 34;

	private RenderSet near;
	private RenderSet far;
	private RenderSet A; 

	private Texture2D atlas;
	private Texture2D whiteTex;
	private Material mat;
	private Material skyMat;

	private Puff[] puffs;
	private Drop[] drops;
	private int canopyN;
	private int cloudN;

	private float flashTimer;
	private float flashCooldown;
	private double flashS;
	private double flashH;
	private float flashPower;

	private Double2 camG;
	private Vector2 camLocal;
	
	private Vector2 alignOrigin;
	
	private bool farAbs;
	private float farAbsS;
	private float renderScale = 1f;
	
	private Vector2 prevCamLocal;
	private Vector2 prevStormLocal;
	private bool hasPrevLoc;
	
	
	
	private bool puffLive = true;
	
	
	public float spawnAnimT = 1f;

	
	public const int ZTestAlways = 0;
	public const int ZTestNormal = 4;
	public const int TopQueueCloud = 3500;
	public const int TopQueueSky = 3400;
	public const int TopSortingBase = 3500;

	
	public static float farSizeScale = 1.5f;     
	public static bool farSizeFine = false;     
	public static float farTilt = 0f;           
	public static float farDepthFrac = 0.012f;  

	
	
	
	public static float downburstGap = 0.3f;      
	public static bool downburstGapFine = false; 

	
	
	
	
	
	public static float rainLenScale = 0.05f;       
	public static float rainWidScale = 0.1f;        
	public static bool rainShapeTarget;           

	
	
	
	
	
	
	
	public static float downburstZoneHoriz = 0.2f;   
	public static float downburstZoneVert = 0.04f;   
	public static float tornadoZoneHoriz = 1f;       
	public static float tornadoZoneVert = 1f;        
	
	public static float windZoneOffA = 0f;           
	public static float windZoneOffB = 0f;           
	public static bool windZoneHalf;                 
	public static bool windZoneFine;                 
	public static bool downburstZoneShow;            

	
	
	
	
	
	
	
	
	public static float[] windZoneGain = new float[11] { 1.2f, 1.2f, 1.2f, 1.2f, 1.2f, 1.0f, 1.2f, 1.2f, 1.2f, 1.2f, 1.2f };
	public static string[] windZoneNames = new string[11] { "外围弱", "较弱", "中", "较强", "强·眼壁", "风眼弱", "强·眼壁", "较强", "中", "较弱", "外围弱" };
	public static bool windGainPanel;                
	public static float gainPanelX = 16f;            
	public static float gainPanelY = 140f;

	
	
	
	private static readonly Color CanopyLow = new Color(0.16f, 0.14f, 0.12f);
	private static readonly Color CanopyHigh = new Color(0.44f, 0.41f, 0.34f);
	private static readonly Color CloudLow = new Color(0.3f, 0.32f, 0.38f);
	private static readonly Color CloudHigh = new Color(0.98f, 0.98f, 1f);

	
	public WeatherSystem storm;
	private WeatherSystem S => storm;

	private void Awake()
	{
		BuildAssets();
	}

	private void BuildAssets()
	{
		atlas = MakeAtlas();
		string[] array = new string[7] { "Sprites/Default", "Legacy Shaders/Particles/Alpha Blended", "Particles/Alpha Blended", "Mobile/Particles/Alpha Blended", "Particles/Standard Unlit", "UI/Default", "Unlit/Transparent" };
		Shader val = null;
		for (int i = 0; i < array.Length; i++)
		{
			if (val != null)
			{
				break;
			}
			val = Shader.Find(array[i]);
		}
		if (val == null)
		{
			SpriteRenderer val2 = UnityEngine.Object.FindObjectOfType<SpriteRenderer>();
			if (val2 != null && val2.sharedMaterial != null)
			{
				val = val2.sharedMaterial.shader;
			}
		}
		if (val == null)
		{
			Debug.LogError((object)"[Typhoon] no usable shader found — visuals disabled");
			TyphoonConfig.I.visuals = false;
			return;
		}
		Debug.Log((object)("[Typhoon] cloud shader = " + val.name));
		mat = new Material(val);
		mat.mainTexture = (Texture)atlas;
		mat.renderQueue = 3000;
		whiteTex = new Texture2D(1, 1);
		whiteTex.SetPixel(0, 0, Color.white);
		whiteTex.Apply();
		skyMat = new Material(val);
		skyMat.mainTexture = (Texture)whiteTex;
		skyMat.renderQueue = 3000;

		
		near = MakeSet("Typhoon Near", false);
		far = MakeSet("Typhoon Far", true);

		Alloc(ref near.sV, ref near.sT, ref near.sC, ref near.sI, 1632);
		Alloc(ref far.sV, ref far.sT, ref far.sC, ref far.sI, 1632);
		ApplyTopMost();
		SetVisibleAll(v: false);
	}

	private RenderSet MakeSet(string prefix, bool scaled)
	{
		RenderSet rs = new RenderSet();
		rs.backOrder = 20;
		
		
		rs.frontOrder = 19;
		rs.skyOrder = 18;
		rs.backGO = NewLayer(prefix + " Clouds", rs.backOrder, out rs.backMesh);
		rs.frontGO = NewLayer(prefix + " Foreground", rs.frontOrder, out rs.frontMesh);
		rs.skyGO = NewLayer(prefix + " Sky", rs.skyOrder, out rs.skyMesh);
		rs.backR = rs.backGO.GetComponent<MeshRenderer>();
		rs.frontR = rs.frontGO.GetComponent<MeshRenderer>();
		rs.skyR = rs.skyGO.GetComponent<MeshRenderer>();
		rs.skyR.sharedMaterial = skyMat;
		int defaultLayer = LayerMask.NameToLayer("Default");
		int scaledLayer = LayerMask.NameToLayer("Scaled Space");
		int layer = scaled ? ((scaledLayer >= 0) ? scaledLayer : defaultLayer) : defaultLayer;
		if (layer < 0)
		{
			layer = 0;
		}
		rs.backGO.layer = layer;
		rs.frontGO.layer = layer;
		rs.skyGO.layer = layer;
		rs.isScaled = scaled;
		if (scaled)
		{
			float s = 0.0001f;
			rs.backGO.transform.localScale = new Vector3(s, s, s);
			rs.frontGO.transform.localScale = new Vector3(s, s, s);
			rs.skyGO.transform.localScale = new Vector3(s, s, s);
		}
		return rs;
	}

	private GameObject NewLayer(string name, int order, out Mesh mesh)
	{
		GameObject val = new GameObject(name);
		UnityEngine.Object.DontDestroyOnLoad((UnityEngine.Object)val);
		val.transform.position = Vector3.zero;
		mesh = new Mesh();
		mesh.MarkDynamic();
		mesh.bounds = new Bounds(Vector3.zero, new Vector3(100000000f, 100000000f, 100000000f));
		val.AddComponent<MeshFilter>().sharedMesh = mesh;
		MeshRenderer obj = val.AddComponent<MeshRenderer>();
		obj.sharedMaterial = mat;
		obj.sortingLayerName = "Default";
		obj.sortingOrder = order;
		obj.shadowCastingMode = ShadowCastingMode.Off;
		obj.receiveShadows = false;
		return val;
	}

	
	
	
	
	
	private void ApplyTopMost()
	{
		if (mat != null)
		{
			try
			{
				mat.SetInt("_ZTest", ZTestAlways);
			}
			catch
			{
			}
			mat.renderQueue = TopQueueCloud;
		}
		if (skyMat != null)
		{
			try
			{
				skyMat.SetInt("_ZTest", ZTestAlways);
			}
			catch
			{
			}
			skyMat.renderQueue = TopQueueSky;
		}
		ApplySorting(near);
		ApplySorting(far);
	}

	private static void ApplySorting(RenderSet rs)
	{
		if (rs == null)
		{
			return;
		}
		if (rs.backR != null)
		{
			rs.backR.sortingOrder = TopSortingBase + rs.backOrder;
		}
		if (rs.frontR != null)
		{
			rs.frontR.sortingOrder = TopSortingBase + rs.frontOrder;
		}
		if (rs.skyR != null)
		{
			rs.skyR.sortingOrder = TopSortingBase + rs.skyOrder;
		}
	}

	
	
	
	
	
	
	private static Camera CameraForLayer(int layer)
	{
		if (layer < 0)
		{
			return null;
		}
		int bit = 1 << layer;
		Camera best = null;
		Camera[] all = Camera.allCameras;
		for (int i = 0; i < all.Length; i++)
		{
			Camera c = all[i];
			if (!(c == null) && c.isActiveAndEnabled && (c.cullingMask & bit) != 0 && (best == null || c.depth > best.depth))
			{
				best = c;
			}
		}
		return best;
	}

	
	
	
	
	
	private static float HalfExtent(Camera cam, out float depth)
	{
		float n = Mathf.Max(cam.nearClipPlane, 0.0001f);
		float f = Mathf.Max(cam.farClipPlane, n * 4f);
		float half;
		if (cam.orthographic)
		{
			half = Mathf.Abs(cam.orthographicSize);
			depth = n + (f - n) * 0.02f;
		}
		else
		{
			depth = Mathf.Clamp(n * 20f, n * 2f, f * 0.5f);
			half = depth * Mathf.Tan(cam.fieldOfView * 0.5f * (float)(Math.PI / 180f));
		}
		if (half <= 0f)
		{
			half = 1f;
		}
		return half;
	}

	
	private static Texture2D MakeAtlas()
	{
		Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
		Color[] px = new Color[128 * 128];
		for (int i = 0; i < 128; i++)
		{
			float v = (i + 0.5f) / 128f;
			for (int j = 0; j < 128; j++)
			{
				float a;
				if (j < 64)
				{
					float nx = (j + 0.5f) / 64f * 2f - 1f;
					float ny = v * 2f - 1f;
					float d = Mathf.Sqrt(nx * nx + ny * ny);
					a = Mathf.Clamp01(1f - d);
					a = a * a * (3f - 2f * a);
					a *= 0.55f + 0.45f * Mathf.PerlinNoise(j * 0.09f, i * 0.09f);
				}
				else
				{
					float nx2 = (j - 64 + 0.5f) / 64f * 2f - 1f;
					float band = Mathf.Clamp01(1f - Mathf.Abs(nx2));
					band *= band;
					float wave = Mathf.Sin(v * Mathf.PI);
					a = band * Mathf.Pow(wave, 0.55f);
				}
				px[i * 128 + j] = new Color(1f, 1f, 1f, a);
			}
		}
		tex.SetPixels(px);
		tex.wrapMode = TextureWrapMode.Repeat;
		tex.filterMode = FilterMode.Bilinear;
		tex.Apply();
		return tex;
	}

	public void Rebuild()
	{
		TyphoonConfig i = TyphoonConfig.I;
		canopyN = Mathf.Clamp(i.canopyPuffs, 0, 4000);
		cloudN = Mathf.Clamp(i.cloudPuffs, 0, 4000);
		
		int num = Mathf.Clamp(i.rainDrops * 3, 0, 12000);
		puffs = new Puff[canopyN + cloudN];
		for (int j = 0; j < canopyN; j++)
		{
			puffs[j] = SpawnPuff(0);
		}
		for (int k = 0; k < cloudN; k++)
		{
			puffs[canopyN + k] = SpawnPuff(1);
		}
		drops = new Drop[num];
		
		
		
		near.backQuads = canopyN + cloudN + 800;
		near.frontQuads = num;
		far.backQuads = canopyN + cloudN + 800;
		far.frontQuads = num;
		Alloc(ref near.bV, ref near.bT, ref near.bC, ref near.bI, near.backQuads);
		Alloc(ref near.fV, ref near.fT, ref near.fC, ref near.fI, near.frontQuads);
		Alloc(ref far.bV, ref far.bT, ref far.bC, ref far.bI, far.backQuads);
		Alloc(ref far.fV, ref far.fT, ref far.fC, ref far.fI, far.frontQuads);
		Push(near.backMesh, near.bV, near.bT, near.bC, near.bI, withIndices: true);
		Push(near.frontMesh, near.fV, near.fT, near.fC, near.fI, withIndices: true);
		Push(far.backMesh, far.bV, far.bT, far.bC, far.bI, withIndices: true);
		Push(far.frontMesh, far.fV, far.fT, far.fC, far.fI, withIndices: true);
	}

	private static void Alloc(ref Vector3[] v, ref Vector2[] t, ref Color[] c, ref int[] idx, int quads)
	{
		v = new Vector3[quads * 4];
		t = new Vector2[quads * 4];
		c = new Color[quads * 4];
		idx = new int[quads * 6];
		for (int i = 0; i < quads; i++)
		{
			int num = i * 4;
			int num2 = i * 6;
			idx[num2] = num;
			idx[num2 + 1] = num + 1;
			idx[num2 + 2] = num + 2;
			idx[num2 + 3] = num;
			idx[num2 + 4] = num + 2;
			idx[num2 + 5] = num + 3;
		}
	}

	private static void Push(Mesh m, Vector3[] v, Vector2[] t, Color[] c, int[] idx, bool withIndices)
	{
		if (m != null)
		{
			if (withIndices)
			{
				m.Clear();
			}
			m.vertices = v;
			m.uv = t;
			m.colors = c;
			if (withIndices)
			{
				m.triangles = idx;
			}
			m.bounds = new Bounds(Vector3.zero, new Vector3(100000000f, 100000000f, 100000000f));
		}
	}

	public void Clear()
	{
		SetVisibleAll(v: false);
	}

	private void SetVisibleAll(bool v)
	{
		SetVisible(near, v);
		SetVisible(far, v);
	}

	private void SetVisible(RenderSet s, bool v)
	{
		if (s == null)
		{
			return;
		}
		if (s.backGO != null && s.backGO.activeSelf != v)
		{
			s.backGO.SetActive(v);
		}
		if (s.frontGO != null && s.frontGO.activeSelf != v)
		{
			s.frontGO.SetActive(v);
		}
		if (s.skyGO != null && s.skyGO.activeSelf != v)
		{
			s.skyGO.SetActive(v);
		}
	}

	private Puff SpawnPuff(int kind)
	{
		Puff result = new Puff
		{
			kind = kind
		};
		double num;
		double num2;
		double num3;
		
		
		
		
		bool isLine = S.type == StormType.SquallLine;
		bool isRotary = S.type == StormType.Typhoon;
		if (kind == 0)
		{
			if (isLine)
			{
				num = Random.Range(0.1f, 4.2f);
				num2 = Math.Pow(Random.value, 0.55) * 0.85 + 0.12;
			}
			else if (isRotary)
			{
				
				
				
				float eyeR0 = (float)WeatherSystem.TyphoonEyeR(S.category);
				if (eyeR0 <= 0f)
				{
					num = Math.Pow(Random.value, 1.6) * 2.4;   
				}
				else
				{
					
					
					if (Random.value < 0.25)
					{
						num = Random.Range(0.02f, eyeR0);
					}
					else
					{
						num = eyeR0 * 0.7f + Random.Range(0f, 3.4f) * (0.25f + 0.75f * Random.value);
					}
				}
				num2 = Math.Pow(Random.value, 0.6) * 0.92 + 0.06;
			}
			else
			{
				
				
				
				
				num = Math.Pow(Random.value, 1.6) * 2.0;
				num2 = Math.Pow(Random.value, 0.7) * 0.9 + 0.1;
			}
			num3 = ((Random.value < 0.5) ? (-1.0) : 1.0);
			
			result.size = (float)(S.Rmax * Random.Range(0.2f, 0.6f));
			result.baseAlpha = Random.Range(0.55f, 0.97f);
			result.maxLife = Random.Range(45f, 140f);
		}
		else
		{
			if (isLine)
			{
				
				num = Random.Range(0.2f, 4.0f);
				num2 = Random.Range(0.12f, 0.95f);
			}
			else if (isRotary)
			{
				
				
				float eyeR1 = (float)WeatherSystem.TyphoonEyeR(S.category);
				if (eyeR1 <= 0f)
				{
					num = Random.Range(0.05f, 1.8f);
				}
				else
				{
					if (Random.value < 0.15)
					{
						num = Random.Range(0.02f, eyeR1 * 0.9f);
					}
					else
					{
						num = (Random.value < 0.6) ? Random.Range(eyeR1 * 0.8f, eyeR1 + 1.2f) : Random.Range(eyeR1 + 1.2f, 4.2f);
					}
				}
				num2 = Random.Range(0.1f, 1.08f);
			}
			else
			{
				
				num = Random.Range(0.05f, 1.55f);
				num2 = Random.Range(0.15f, 1.05f);
			}
			num3 = ((Random.value < 0.5) ? (-1.0) : 1.0);
			result.size = (float)(S.Rmax * Random.Range(0.05f, 0.18f) * (0.6 + 0.7 * num2));
			result.baseAlpha = Random.Range(0.3f, 0.85f);
			result.maxLife = Random.Range(26f, 70f);
		}
		
		result.pos = FromStorm(num3 * num * S.Rmax, S.Hbase + num2 * (S.Htop - S.Hbase));
		result.life = result.maxLife * Random.value;
		result.seed = Random.value;
		return result;
	}

	private Double2 FromStorm(double s, double h)
	{
		double num = (S.planet != null) ? S.planet.Radius : 1.0;
		double num2 = S.centerAngle + s / num;
		double num3 = num + h;
		return new Double2(Math.Cos(num2) * num3, Math.Sin(num2) * num3);
	}

	private void LateUpdate()
	{
		WeatherSystem s = S;
		if (s == null || !s.active || s.planet == null || !TyphoonConfig.I.visuals)
		{
			SetVisibleAll(v: false);
			return;
		}
		if (puffs == null || puffs.Length != Mathf.Clamp(TyphoonConfig.I.canopyPuffs, 0, 4000) + Mathf.Clamp(TyphoonConfig.I.cloudPuffs, 0, 4000))
		{
			Rebuild();
		}
		WorldView main = WorldView.main;
		if (main == null)
		{
			SetVisibleAll(v: false);
			return;
		}
		Camera main2 = Camera.main;
		if (main2 == null)
		{
			SetVisibleAll(v: false);
			return;
		}
		Location playerLocation = TyphoonManager.GetPlayerLocation();
		if (playerLocation == null || playerLocation.planet != (object)s.planet)
		{
			SetVisibleAll(v: false);
			return;
		}
		float dt = Mathf.Min(Time.deltaTime, 0.2f);
		UpdateLightning(dt);
		
		
		if (spawnAnimT < 1f)
		{
			spawnAnimT = Mathf.Min(1f, spawnAnimT + dt / 2f);
		}
		if (s.RebuildPuffsFlag)
		{
			s.RebuildPuffsFlag = false;
			Rebuild();
			spawnAnimT = 0f;
		}

		Vector3 position = main2.transform.position;
		camG = WorldView.ToGlobalPosition(new Vector2(position.x, position.y));
		camLocal = WorldView.ToLocalPosition(camG);
		
		
		try
		{
			s.ToStormFrame(camG, out var psLive, out var phLive);
			puffLive = Math.Abs(psLive) < s.Rmax * 6.0 && phLive < s.Htop * 1.4;
		}
		catch
		{
			puffLive = false;
		}

		
		
		
		Vector2 stormLocalNow = Vector2.zero;
		if (s != null && s.planet != null)
		{
			stormLocalNow = WorldView.ToLocalPosition(FromStorm(0.0, 0.0));
		}
		bool teleporting = false;
		if (hasPrevLoc)
		{
			float camJump = (camLocal - prevCamLocal).magnitude;
			float stormJump = (stormLocalNow - prevStormLocal).magnitude;
			teleporting = camJump > 200000f || stormJump > 200000f;
		}
		prevCamLocal = camLocal;
		prevStormLocal = stormLocalNow;
		hasPrevLoc = true;
		if (teleporting)
		{
			SetVisibleAll(v: false);
			return;
		}

		
		float vd = 0f;
		try
		{
			vd = main.viewDistance.Value;
		}
		catch
		{
		}
		bool scaledSpace = main.scaledSpace.Value;
		bool farActive = (vd >= 50000f || scaledSpace);

		float rs = farActive ? 0.0001f : 1f;

		int defaultLayer = LayerMask.NameToLayer("Default");
		int scaledLayer = LayerMask.NameToLayer("Scaled Space");
		int layer = farActive ? ((scaledLayer >= 0) ? scaledLayer : defaultLayer) : defaultLayer;
		if (layer < 0)
		{
			layer = 0;
		}

		
		A = farActive ? far : near;
		SetVisible(near, !farActive);
		SetVisible(far, farActive);

		A.backGO.layer = layer;
		A.frontGO.layer = layer;
		A.skyGO.layer = layer;

		
		
		float mainHalf = (main2.orthographic ? main2.orthographicSize : (Mathf.Abs(main2.transform.position.z) * Mathf.Tan(main2.fieldOfView * 0.5f * (float)(Math.PI / 180f))));
		if (mainHalf < 1f)
		{
			mainHalf = 1f;
		}

		Vector2 val = Vector2.zero;
		if (s != null && s.planet != null)
		{
			val = WorldView.ToLocalPosition(s.MergedStormC());   
		}

		
		
		Camera camL = CameraForLayer(layer);

		bool align;
		float geoHalf;
		float geoRs;
		float goScale;
		Vector3 anchor;
		Quaternion anchorRot = Quaternion.identity;
		Camera geoCam = main2;
		bool viewportPath = farActive && camL != null;
		if (viewportPath)
		{
			
			
			float worldHalf = ((vd > 1f) ? vd : mainHalf);
			float depth;
			float halfCam = HalfExtent(camL, out depth);
			geoHalf = worldHalf;
			geoRs = 1f;
			goScale = 1f;
			Vector2 planetXY = Vector2.zero;
			try
			{
				double wt = WorldTime.main.worldTime;
				Double2 stormSolar = s.planet.GetSolarSystemPosition(wt) + (Double2)s.MergedStormC();   
				Double2 viewSolar = WorldView.main.ViewLocation.GetSolarSystemPosition(wt);
				Double2 diff = (stormSolar - viewSolar) / 10000.0;
				planetXY = new Vector2((float)diff.x, (float)diff.y);
			}
			catch
			{
				planetXY = new Vector2(val.x / 10000f, val.y / 10000f);
			}
			
			anchor = new Vector3(planetXY.x, planetXY.y, vd / 10000f);
			anchorRot = Quaternion.identity;
			farAbs = true;
			farAbsS = farSizeScale * halfCam / Mathf.Max(depth, 0.0001f);
			align = true;
			alignOrigin = val;
			geoCam = camL;
		}
		else
		{
			
			align = false;
			geoHalf = mainHalf;
			geoRs = 1f;
			goScale = 1f;
			anchor = (Vector3)(rs * camLocal);
			anchorRot = Quaternion.identity;
			farAbs = false;
			geoCam = main2;
		}

		renderScale = goScale;
		
		Vector3 goScaleV = (farAbs ? Vector3.one : new Vector3(goScale, goScale, goScale));
		Quaternion goRot = (farAbs ? Quaternion.identity : anchorRot);
		A.backGO.transform.localScale = goScaleV;
		A.frontGO.transform.localScale = goScaleV;
		A.skyGO.transform.localScale = goScaleV;
		A.backGO.transform.position = anchor;
		A.frontGO.transform.position = anchor;
		A.skyGO.transform.position = anchor;
		A.backGO.transform.rotation = goRot;
		A.frontGO.transform.rotation = goRot;
		A.skyGO.transform.rotation = goRot;

		
		ApplyTopMost();
		BuildBack(dt, geoHalf, geoRs, align);
		BuildRain(dt, geoCam, geoHalf, align);
		RenderSkyLayer(geoCam, geoHalf, geoRs, align);
		Push(A.backMesh, A.bV, A.bT, A.bC, A.bI, withIndices: false);
		Push(A.frontMesh, A.fV, A.fT, A.fC, A.fI, withIndices: false);
	}

	private void RenderSkyLayer(Camera cam, float half, float rs, bool align)
	{
		try
		{
			WeatherSystem s = S;
			s.ToStormFrame(camG, out var s2, out var h);
			double num = Math.Abs(s2) / s.Rmax;
			double num2 = Smooth01((4.6 - num) / 1.6);
			num2 *= 1.0 - WeatherSystem.Clamp01((h - s.Htop) / (s.Htop * 0.4));
			if (num2 < 0.02)
			{
				if (A.skyGO.activeSelf)
				{
					A.skyGO.SetActive(false);
				}
				return;
			}
			if (!A.skyGO.activeSelf)
			{
				A.skyGO.SetActive(true);
			}
			float num3 = Mathf.Clamp((float)(s.Vmax / 78.0), 0.5f, 1f);
			Color canopyLow = CanopyLow;
			Color botBase = Color.Lerp(CanopyLow, CanopyHigh, num3 * 0.6f);
			
			
			float str = Mathf.Clamp01((float)(s.Vmax / 45.0));
			float maxOp = Mathf.Lerp((float)TyphoonConfig.I.skyOpacity * 0.5f, 1f, str);
			float op = maxOp * (float)num2 * (float)S.MergeFade();   
			float num4 = (cam.aspect > 0.1f) ? cam.aspect : 1f;
			float num5 = half / rs * num4 * 1.2f;
			float num6 = half / rs * 1.2f;
			if (farAbs)
			{
				
				num5 *= farAbsS / 10000f;
				num6 *= farAbsS / 10000f;
			}
			Vector2 cc = (align ? Vector2.zero : camLocal);
			float num7 = 2f * num5 / 48f;
			float num8 = 2f * num6 / 34f;
			Double2 val = camG;
			Double2 normalized = val.normalized;
			val = camG;
			double magnitude = val.magnitude;
			double radius = s.planet.Radius;
			double soft = 0.03;
			int num9 = 0;
			for (int i = 0; i < 34; i++)
			{
				float num10 = 0f - num6 + (float)i * num8;
				float y = num10 + num8;
				for (int j = 0; j < 48; j++)
				{
					float num11 = 0f - num5 + (float)j * num7;
					float x = num11 + num7;
					WriteSkyCell(num9, cc, num11, num10, x, y, normalized, magnitude, radius, soft, op, canopyLow, botBase, num6);
					num9++;
				}
			}
			Push(A.skyMesh, A.sV, A.sT, A.sC, A.sI, withIndices: true);
		}
		catch
		{
			if (A.skyGO != null)
			{
				A.skyGO.SetActive(false);
			}
		}
	}

	private void WriteSkyCell(int q, Vector2 cc, float x0, float y0, float x1, float y1, Double2 upDir, double camMag, double R, double soft, float op, Color topBase, Color botBase, float extY)
	{
		int num = q * 4;
		A.sV[num] = new Vector3(cc.x + x0, cc.y + y0, 0f);
		A.sV[num + 1] = new Vector3(cc.x + x0, cc.y + y1, 0f);
		A.sV[num + 2] = new Vector3(cc.x + x1, cc.y + y1, 0f);
		A.sV[num + 3] = new Vector3(cc.x + x1, cc.y + y0, 0f);
		A.sT[num] = Vector2.zero;
		A.sT[num + 1] = new Vector2(0f, 1f);
		A.sT[num + 2] = Vector2.one;
		A.sT[num + 3] = new Vector2(1f, 0f);
		A.sC[num] = SkyVertWorld(new Double2((double)x0, (double)y0), upDir, camMag, R, soft, op, topBase, botBase, extY, y0);
		A.sC[num + 1] = SkyVertWorld(new Double2((double)x0, (double)y1), upDir, camMag, R, soft, op, topBase, botBase, extY, y1);
		A.sC[num + 2] = SkyVertWorld(new Double2((double)x1, (double)y1), upDir, camMag, R, soft, op, topBase, botBase, extY, y1);
		A.sC[num + 3] = SkyVertWorld(new Double2((double)x1, (double)y0), upDir, camMag, R, soft, op, topBase, botBase, extY, y0);
		int num2 = q * 6;
		A.sI[num2] = num;
		A.sI[num2 + 1] = num + 1;
		A.sI[num2 + 2] = num + 2;
		A.sI[num2 + 3] = num;
		A.sI[num2 + 4] = num + 2;
		A.sI[num2 + 5] = num + 3;
	}

	private static Color SkyVertWorld(Double2 V, Double2 upDir, double camMag, double R, double soft, float op, Color topBase, Color botBase, float extY, float oy)
	{
		double num = Math.Sqrt(V.x * V.x + V.y * V.y);
		double num2;
		if (num < 0.001)
		{
			num2 = 0.0;
		}
		else
		{
			double num3 = (V.x * upDir.x + V.y * upDir.y) / num;
			double num4 = Math.Sqrt(Math.Max(0.0, 1.0 - num3 * num3));
			double num5 = R / camMag;
			num2 = Smooth01((num4 - num5) / soft);
		}
		float num6 = Mathf.Clamp01((oy + extY) / (2f * extY));
		Color val = Color.Lerp(botBase, topBase, num6);
		return new Color(val.r, val.g, val.b, op * (float)num2);
	}

	private void UpdateLightning(float dt)
	{
		if (!TyphoonConfig.I.lightning)
		{
			flashPower = 0f;
			return;
		}
		flashTimer -= dt;
		flashCooldown -= dt;
		if (flashTimer > 0f)
		{
			flashPower = Mathf.Max(0f, flashTimer / 0.22f);
			flashPower *= ((Random.value < 0.35f) ? 0.45f : 1f);
			return;
		}
		flashPower = 0f;
		if (flashCooldown <= 0f)
		{
			double num = Mathf.Max(0.35f, (float)(S.Vmax / 78.0));
			flashCooldown = Random.Range(1.4f, 6.5f) / (float)num;
			flashTimer = 0.22f;
			double num2 = ((Random.value < 0.5) ? (-1.0) : 1.0);
			flashS = num2 * S.Rmax * Random.Range(0.8f, 2.6f);
			flashH = S.Htop * Random.Range(0.15f, 0.6f);
		}
	}

	private void BuildBack(float dt, float camHalf, float rs, bool align)
	{
		WeatherSystem s = S;
		float num = (float)TyphoonConfig.I.cloudOpacity;
		int num2 = 0;
		
		
		
		
		int phenomenaQuads = A.backQuads - puffs.Length;
		for (int i = 0; i < puffs.Length; i++)
		{
			Puff puff = puffs[i];
			Double2 val = s.SampleWind(puff.pos);
			ref Double2 pos = ref puff.pos;
			pos += val * ((puff.kind == 0) ? ((double)dt * 0.12) : ((double)dt));
			
			if (puffLive)
			{
				puff.life -= dt;
			}
			s.ToStormFrame(puff.pos, out var s2, out var h);
			double num3 = Math.Abs(s2) / s.Rmax;
		
		double vSpan = Math.Max(1.0, s.Htop - s.Hbase);
		double num4 = (h - s.Hbase) / vSpan;
		
		if (puffLive && (puff.life <= 0f || num4 < 0.04 || num4 > 1.2 || num3 > 4.6))
		{
			puff = SpawnPuff(puff.kind);
			s.ToStormFrame(puff.pos, out s2, out h);
			num3 = Math.Abs(s2) / s.Rmax;
			num4 = (h - s.Hbase) / vSpan;
		}
			
			
			
			
			
			
			
			
			bool isRot = s.type == StormType.Typhoon;
			bool isLine = s.type == StormType.SquallLine;
			double num6;
			if (isRot)
			{
				
				
				
				
				float num5 = (float)WeatherSystem.TyphoonEyeR(s.category);
				if (num5 <= 0f)
				{
					num6 = 1.0 - Smooth01((num3 - 0.45) / 1.2);   
				}
				else if (num3 < (double)num5)
				{
					double eyeHaze = 0.15 - s.category * 0.022;
					if (eyeHaze < 0.03)
					{
						eyeHaze = 0.03;
					}
					
					num6 = eyeHaze * (0.4 + 0.6 * Smooth01(num3 / (double)num5));
				}
				else
				{
					num6 = Smooth01((num3 - (double)num5) / 0.45);   
				}
			}
			else if (isLine)
			{
				double front = Math.Exp(0.0 - WeatherSystem.Pow2((num3 - 0.9) / 0.5));
				num6 = 0.35 + 0.65 * front;
			}
			else
			{
				
				
				double wCell;
				if (s.type == StormType.Supercell)
				{
					wCell = 0.9;
				}
				else if (s.type == StormType.MCS)
				{
					wCell = 1.9;
				}
				else if (s.type == StormType.Multicell)
				{
					wCell = 1.45;
				}
				else
				{
					wCell = 1.7;
				}
				num6 = 1.0 - Smooth01((num3 - 0.35) / wCell);
			}
			if (!isRot)
			{
				double edgeBand = Math.Exp(0.0 - WeatherSystem.Pow2((num6 - 0.5) / 0.42));   
				num6 *= 1.0 - 0.5 * edgeBand * (0.5 + 0.5 * Math.Sin((double)puff.seed * 6.283 + num3 * 9.0));
			}
			double num7 = Math.Exp(0.0 - WeatherSystem.Pow2(Math.Max(0.0, num3 - (0.7 + 0.9 * num4)) / 3.4));
			double num8 = Mathf.Clamp01(puff.life / 4f) * Mathf.Clamp01((puff.maxLife - puff.life) / 4f);
			double num9 = (double)puff.baseAlpha * num6 * num7 * num8 * (double)num * (double)s.MergeFade()   
				* (double)spawnAnimT * (1.0 - 0.5 * s.TransitionBlend());   
			Color val2;
			if (puff.kind == 0)
			{
				val2 = Color.Lerp(CanopyLow, CanopyHigh, (float)WeatherSystem.Clamp01(num4 * 1.1));
			}
			else
			{
				double num10 = 0.55 + 0.45 * Math.Sin(num3 * 2.6 - s.age * 0.22 + (double)puff.seed * 6.283);
				num9 *= num10;
				if (num4 > 0.8)
				{
					num9 *= 0.55 + 0.45 * Math.Exp(0.0 - WeatherSystem.Pow2((num4 - 0.9) / 0.25));
				}
				val2 = Color.Lerp(CloudLow, CloudHigh, (float)WeatherSystem.Clamp01(num4 * 1.25));
			}
			if (num9 < 0.004)
			{
				num9 = 0.0;
			}
			if (flashPower > 0.001f)
			{
				double num11 = Math.Sqrt(WeatherSystem.Pow2((s2 - flashS) / (s.Rmax * 1.3)) + WeatherSystem.Pow2((h - flashH) / (s.Htop * 0.45)));
				float num12 = flashPower * (float)Math.Exp((0.0 - num11) * num11);
				if (num12 > 0.002f)
				{
					val2 = Color.Lerp(val2, new Color(1f, 0.97f, 0.85f), Mathf.Clamp01(num12 * 1.6f));
					num9 = Math.Min(1.0, num9 + (double)num12 * 0.5);
				}
			}
			
			
			
			double whiten = (1.0 - num6) * 0.6;
			if (whiten > 0.02)
			{
				val2 = Color.Lerp(val2, Color.white, (float)Math.Min(0.9, whiten));
			}
			val2.a = (float)num9;
			Vector2 val3 = WorldView.ToLocalPosition(puff.pos);
			float num13 = puff.size * rs;
			
			
			if (puff.kind == 1 && num6 < 0.9)
			{
				num13 *= (float)(0.45 + 0.55 * (num6 / 0.9));
			}
			float num14 = camHalf * 6f;
			if (num13 > num14)
			{
				num13 = num14;
			}
			float num15 = 2f;
			if (num13 < num15)
			{
				num13 = num15;
			}
			float num16 = num13 / rs;
			
			
			if (i + phenomenaQuads < A.backQuads)
			{
				
				Vector2 centre = align ? (val3 - alignOrigin) : val3;
				float quadHalf = num16;
				if (farAbs)
				{
					centre = (val3 - alignOrigin) * (farAbsS / 10000f);
					quadHalf = num16 * farAbsS / 10000f;
				}
				WriteQuad(A.bV, A.bT, A.bC, i + phenomenaQuads, centre, quadHalf, quadHalf, Vector2.right, val2, 0f);
			}
			puffs[i] = puff;
		}
		
		
		
		
		
		
		if (S != null && S.planet != null && num2 < A.backQuads)
		{
			for (int fi = 0; fi < S.tornadoes.Count; fi++)
			{
				WeatherSystem.FxInst fx = S.tornadoes[fi];
				if (fx.strength <= 0.05 || Math.Min(fx.phase, fx.strength) <= 0.04)
				{
					continue;
				}
				
				
				Double2 stormC = FxAnchor(S, fx);   
				Double2 radialP = stormC.normalized;
				Double2 perpP = new Double2(0.0 - radialP.y, radialP.x);
				double grow = Math.Min(fx.phase, fx.strength);
				
				
				double torRise = (S.Htop - S.Hbase) * 0.08;
				
				
				
				
				
				
				int layers = 14;
				for (int li = 0; li < layers && num2 < A.backQuads; li++)
				{
					double t = (double)li / (double)(layers - 1);
					double hh = (S.Hbase + torRise) * (1.0 - grow * (1.0 - t));
					
					double rr = S.Rmax * (0.015 + 0.07 * t);
					if (t > 0.8)
					{
						rr += S.Rmax * (t - 0.8) * 0.9;
					}
					Double2 planetPos = stormC + radialP * hh;   
					Vector2 cen = WorldView.ToLocalPosition(planetPos);   
					Vector2 qc = align ? (cen - alignOrigin) : cen;
					float halfW = (float)rr;
					float halfC = (float)(rr * 0.65);
					if (farAbs)
					{
						qc = (cen - alignOrigin) * (farAbsS / 10000f);
						halfW = halfW * farAbsS / 10000f;
						halfC = halfC * farAbsS / 10000f;
					}
					
					
					
					
					float aWall = 0.95f * (0.52f + 0.55f * (float)t) * (0.4f + 0.6f * (float)grow) * S.MergeFade();   
					WriteQuad(A.bV, A.bT, A.bC, num2++, qc, halfW, halfW, Vector2.right, new Color(0.6f, 0.66f, 0.82f, aWall), 0f);
					float aCore = 0.32f * (0.5f + 0.5f * (float)t) * (0.4f + 0.6f * (float)grow) * S.MergeFade();   
					WriteQuad(A.bV, A.bT, A.bC, num2++, qc, halfC, halfC, Vector2.right, new Color(0.82f, 0.88f, 1f, aCore), 0f);
				}
			}
		}
		
		
		
		
		
		if (S != null && S.planet != null && num2 < A.backQuads)
		{
			for (int fi = 0; fi < S.tornadoes.Count; fi++)
			{
				WeatherSystem.FxInst fx = S.tornadoes[fi];
				if (fx.strength <= 0.05 || Math.Min(fx.phase, fx.strength) <= 0.04)
				{
					continue;
				}
				Double2 stormC = FxAnchor(S, fx);   
				Double2 radialP = stormC.normalized;                       
				Double2 perpP = new Double2(0.0 - radialP.y, radialP.x);   
				double grow = Math.Min(fx.phase, fx.strength);
				double torRise = (S.Htop - S.Hbase) * 0.08;
				
				double hhBase = (S.Hbase + torRise) * (1.0 - grow);
				int dustN = 8;
				for (int d = 0; d < dustN && num2 < A.backQuads; d++)
				{
					double ang = S.age * 5.0 + (double)d * 0.785;
					
					double dustR = S.Rmax * (0.04 + 0.05 * (0.5 + 0.5 * Math.Sin(S.age * 2.5 + (double)d)));
					double hhD = hhBase + S.Rmax * 0.025 * Math.Abs(Math.Sin(S.age * 3.5 + (double)d * 1.3));
					Double2 planetPos = stormC + radialP * hhD + perpP * (dustR * Math.Cos(ang));   
					Vector2 cen = WorldView.ToLocalPosition(planetPos);
					Vector2 qc = align ? (cen - alignOrigin) : cen;
					float half = (float)(S.Rmax * 0.026);
					if (farAbs)
					{
						qc = (cen - alignOrigin) * (farAbsS / 10000f);
						half = half * farAbsS / 10000f;
					}
					
					
					
					float a = 0.72f * (0.4f + 0.6f * (float)grow) * (0.55f + 0.45f * (float)Math.Abs(Math.Sin(S.age * 3.0 + (double)d))) * S.MergeFade();   
					WriteQuad(A.bV, A.bT, A.bC, num2++, qc, half, half, Vector2.right, new Color(0.82f, 0.74f, 0.62f, a), 0f);
				}
			}
		}
		
		
		
		
		
		if (S != null && S.planet != null && num2 < A.backQuads)
		{
			for (int fi = 0; fi < S.tornadoes.Count; fi++)
			{
				WeatherSystem.FxInst fx = S.tornadoes[fi];
				if (fx.strength <= 0.05 || Math.Min(fx.phase, fx.strength) <= 0.04)
				{
					continue;
				}
				Double2 stormC = FxAnchor(S, fx);   
				Double2 radialP = stormC.normalized;                       
				Double2 perpP = new Double2(0.0 - radialP.y, radialP.x);   
				double grow = Math.Min(fx.phase, fx.strength);
				
				double torRise = (S.Htop - S.Hbase) * 0.08;
				
				int spirN = 16;
				for (int si = 0; si < spirN && num2 < A.backQuads; si++)
				{
					double uu = (double)((si + S.age * 3.0) % (double)spirN) / (double)spirN;
					double hh = (S.Hbase + torRise) * (1.0 - grow * (1.0 - uu));
					double helixR = S.Rmax * (0.04 + 0.065 * uu);   
					double ang2 = S.age * 7.0 + uu * 34.0 + (double)si;
					double sway2 = 0.06 * Math.Sin(S.age * 2.2 + (double)si * 1.7);  
					Double2 planetPos = stormC + radialP * hh + perpP * (helixR * Math.Cos(ang2) + sway2 * S.Rmax);   
					Vector2 cen = WorldView.ToLocalPosition(planetPos);
					Vector2 qc = align ? (cen - alignOrigin) : cen;
					float half = (float)(S.Rmax * 0.012);
					if (farAbs)
					{
						qc = (cen - alignOrigin) * (farAbsS / 10000f);
						half = half * farAbsS / 10000f;
					}
					float a = 0.85f * (0.5f + 0.5f * (float)grow) * (1f - 0.35f * (float)uu) * S.MergeFade();   
					WriteQuad(A.bV, A.bT, A.bC, num2++, qc, half, half, Vector2.right, new Color(0.88f, 0.93f, 1f, a), 0f);
				}
			}
		}
		
		
		
		
		
		if (S != null && S.planet != null && num2 < A.backQuads)
		{
			for (int fi = 0; fi < S.tornadoes.Count; fi++)
			{
				WeatherSystem.FxInst fx = S.tornadoes[fi];
				if (fx.strength <= 0.05 || Math.Min(fx.phase, fx.strength) <= 0.04)
				{
					continue;
				}
				Double2 stormC = FxAnchor(S, fx);   
				Double2 radialP = stormC.normalized;                       
				Double2 perpP = new Double2(0.0 - radialP.y, radialP.x);   
				double grow = Math.Min(fx.phase, fx.strength);
				
				double torRise = (S.Htop - S.Hbase) * 0.08;
				int debN = 20;
				for (int di2 = 0; di2 < debN && num2 < A.backQuads; di2++)
				{
					double uu = (double)((di2 + S.age * 4.0) % (double)debN) / (double)debN;
					double hh = (S.Hbase + torRise) * (1.0 - grow * (1.0 - uu));   
					double dR = S.Rmax * (0.02 + 0.09 * uu);
					double ang3 = S.age * 9.0 + uu * 26.0 + (double)di2 * 2.2;
					double sway3 = 0.05 * Math.Sin(S.age * 3.0 + (double)di2) * S.Rmax;
					Double2 planetPos = stormC + radialP * hh + perpP * (dR * Math.Cos(ang3) + sway3);   
					Vector2 cen = WorldView.ToLocalPosition(planetPos);
					Vector2 qc = align ? (cen - alignOrigin) : cen;
					float half = (float)(S.Rmax * 0.01);
					if (farAbs)
					{
						qc = (cen - alignOrigin) * (farAbsS / 10000f);
						half = half * farAbsS / 10000f;
					}
					float a = 0.85f * (float)grow * (1f - 0.35f * (float)uu) * S.MergeFade();   
					WriteQuad(A.bV, A.bT, A.bC, num2++, qc, half, half, Vector2.right, new Color(0.66f, 0.52f, 0.36f, a), 0f);
				}
			}
		}
		
		
		
		if (S != null && S.planet != null && num2 < A.backQuads)
		{
			for (int fdi = 0; fdi < S.downbursts.Count; fdi++)
			{
				WeatherSystem.FxInst fx = S.downbursts[fdi];
				if (fx.strength <= 0.05)
				{
					continue;
				}
				Double2 stormC = FxAnchor(S, fx);   
				Double2 radialP = stormC.normalized;                       
				Double2 perpP = new Double2(0.0 - radialP.y, radialP.x);   
				double fall = (S.age * 0.9) % 1.0;
				double ph2 = fx.phase;
			
			
			
			int dropsN = 64;
			int waves = 8;
			int perWave = dropsN / waves;
			for (int di = 0; di < dropsN && num2 < A.backQuads; di++)
			{
				int wi = di / perWave;            
				int pi = di % perWave;            
				
				double tt = ((double)wi / (double)waves + fall) % 1.0;
				
				
				
				double hh = (S.Hbase + (S.Htop - S.Hbase) * 0.2) * (1.0 - tt);
				
				
				
				double spread = Smooth(WeatherSystem.Clamp01((tt - 0.96) / 0.04));
				if (spread > 0.01)
				{
					hh = 0.0;   
				}
				
				double lineW = S.Rmax * downburstGap;
				double offsetX = ((double)pi / (double)(perWave - 1) - 0.5) * lineW;
				
				
				double offsetFinal = offsetX * (1.0 + spread * 4.0);
				
				Double2 centerP = stormC + radialP * hh;   
				Double2 pPlanet = centerP + perpP * offsetFinal + radialP * 0.0;
				if (BlockedByPlanet(camG, pPlanet, S.planet.Radius))
				{
					continue;
				}
				
				
				float born = Mathf.Clamp01((float)(tt / 0.12));
				float fade = 1f - (float)spread;
				float a = 0.9f * (float)fx.strength * (float)ph2 * born * (0.25f + 0.75f * fade) * S.MergeFade();   
				if (a < 0.02f)
				{
					continue;   
				}
				
				
				Double2 planetPos = stormC + radialP * hh + perpP * offsetFinal;
				Vector2 cen = WorldView.ToLocalPosition(planetPos);
				Vector2 qc = align ? (cen - alignOrigin) : cen;
				float half = (float)(S.Rmax * 0.045);   
				if (farAbs)
				{
					qc = (cen - alignOrigin) * (farAbsS / 10000f);
					half = half * farAbsS / 10000f;
				}
				WriteQuad(A.bV, A.bT, A.bC, num2++, qc, half, half, Vector2.right, new Color(0.72f, 0.76f, 0.85f, a), 0f);
			}
			}
		}
		
		
		
		
		
		if (downburstZoneShow && S != null && S.planet != null && num2 + 20 < phenomenaQuads)
		{
			Double2 cZone = S.MergedStormC();
			Double2 radialP = cZone.normalized;
			Double2 pDir = new Double2(0.0 - radialP.y, radialP.x);
			double[] bounds = { 0.7, 1.2, 1.6, 2.2, 3.0 };
			for (int bi = 0; bi < 5; bi++)
			{
				float al = (bi < 2) ? 0.95f : 0.45f;
				
				DrawZoneTick(cZone, pDir, (bounds[bi] + (double)StormRenderer.windZoneOffA) * S.Rmax, S.Rmax, al, align, phenomenaQuads, ref num2);
				DrawZoneTick(cZone, pDir, (0.0 - (bounds[bi] + (double)StormRenderer.windZoneOffB)) * S.Rmax, S.Rmax, al, align, phenomenaQuads, ref num2);
			}
			
			
			
			double[] crs = { S.WindCircleRo(13.9), S.WindCircleRo(24.5), S.WindCircleRo(32.7) };
			float[] cal = { 0.95f, 0.6f, 0.95f };
			for (int ci = 0; ci < 3; ci++)
			{
				if (crs[ci] > 0.25)
				{
					DrawZoneTick(cZone, pDir, (crs[ci] + (double)StormRenderer.windZoneOffA) * S.Rmax, S.Rmax, cal[ci], align, phenomenaQuads, ref num2);
					DrawZoneTick(cZone, pDir, (0.0 - (crs[ci] + (double)StormRenderer.windZoneOffB)) * S.Rmax, S.Rmax, cal[ci], align, phenomenaQuads, ref num2);
				}
			}
		}
		
		
		HideRest(A.bV, A.bC, puffs.Length + phenomenaQuads, A.backQuads);
	}

	
	
	private void DrawZoneRect(Double2 center, double halfLen, float alpha, bool align, int quadCap, ref int num2)
	{
		Vector2 cLocal = WorldView.ToLocalPosition(center);
		Vector2 c0 = cLocal;
		Double2 radialP = center.normalized;
		Vector2 rDir = new Vector2((float)radialP.x, (float)radialP.y);
		Vector2 pDir = new Vector2(0f - rDir.y, rDir.x);
		float L = (float)halfLen;
		Vector2 p0 = c0 + rDir * L + pDir * L;
		Vector2 p1 = c0 + rDir * L - pDir * L;
		Vector2 p2 = c0 - rDir * L - pDir * L;
		Vector2 p3 = c0 - rDir * L + pDir * L;
		float halfW = L * 0.008f;   
		DrawEdge(p0, p1, halfW, alpha, align, quadCap, ref num2);
		DrawEdge(p1, p2, halfW, alpha, align, quadCap, ref num2);
		DrawEdge(p2, p3, halfW, alpha, align, quadCap, ref num2);
		DrawEdge(p3, p0, halfW, alpha, align, quadCap, ref num2);
	}

	private void DrawEdge(Vector2 a, Vector2 b, float halfW, float alpha, bool align, int quadCap, ref int num2)
	{
		if (num2 >= quadCap)
		{
			return;
		}
		Vector2 mid = (a + b) * 0.5f;
		Vector2 axis = (b - a).normalized;
		float halfH = (b - a).magnitude * 0.5f;
		Vector2 qc = align ? (mid - alignOrigin) : mid;
		float hw = halfW;
		float hh = halfH;
		if (farAbs)
		{
			qc = (mid - alignOrigin) * (farAbsS / 10000f);
			hw = hw * farAbsS / 10000f;
			hh = hh * farAbsS / 10000f;
		}
		WriteQuad(A.bV, A.bT, A.bC, num2++, qc, hw, hh, axis, new Color(0f, 0f, 0f, alpha), 0f);
	}

	
	
	private static Double2 FxAnchor(WeatherSystem s, WeatherSystem.FxInst fx)
	{
		Double2 c = s.MergedStormC();
		Double2 rp = c.normalized;
		Double2 pd = new Double2(0.0 - rp.y, rp.x);
		return c + pd * (fx.sOff * s.Rmax);
	}

	
	
	private void DrawZoneTick(Double2 center, Double2 pDir, double sPos, double Rmax, float alpha, bool align, int quadCap, ref int num2)
	{
		Double2 pos = center + pDir * sPos;
		Vector2 cLocal = WorldView.ToLocalPosition(pos);
		Double2 radialP = pos.normalized;
		Vector2 rDir = new Vector2((float)radialP.x, (float)radialP.y);
		double lineHalf = Rmax * 1.5;   
		double lineW = Rmax * 0.03;     
		Vector2 p0 = cLocal + rDir * (float)lineHalf;
		Vector2 p1 = cLocal - rDir * (float)lineHalf;
		DrawEdge(p0, p1, (float)(lineW * 0.5), alpha, align, quadCap, ref num2);
	}

	
	private static bool BlockedByPlanet(Double2 camPos, Double2 p, double R)
	{
		Double2 V = p - camPos;
		double a = Double2.Dot(V, V);
		if (a < 0.0001)
		{
			return false;
		}
		double b = 2.0 * Double2.Dot(camPos, V);
		double c = Double2.Dot(camPos, camPos) - R * R;
		double disc = b * b - 4.0 * a * c;
		if (disc <= 0.0)
		{
			return false;
		}
		double sq = Math.Sqrt(disc);
		double t1 = (0.0 - b - sq) / (2.0 * a);
		return t1 > 0.001 && t1 < 1.0;
	}

	private static double Smooth(double x)
	{
		double t = WeatherSystem.Clamp01(x);
		return t * t * (3.0 - 2.0 * t);
	}

	private void BuildRain(float dt, Camera cam, float camHalf, bool align)
	{
		WeatherSystem s = S;
		if (drops == null || drops.Length == 0)
		{
			HideRest(A.fV, A.fC, 0, A.frontQuads);
			return;
		}
		
		
		
		try
		{
			double vd = ((Obs<float>)(object)WorldView.main.viewDistance).Value;
			double rainCut = (S.type == StormType.Cell || S.type == StormType.Multicell) ? 2500.0 : 1500.0;
			if (vd > rainCut)
			{
				HideRest(A.fV, A.fC, 0, A.frontQuads);
				return;
			}
		}
		catch
		{
		}
		Double2 val = camG;
		s.ToStormFrame(val, out var s2, out var h);
		
		
		try
		{
			double distMe = Math.Abs(s2);
			for (int si = 0; si < TyphoonManager.systems.Count; si++)
			{
				WeatherSystem ws = TyphoonManager.systems[si];
				if (ws == null || !ws.active || ws.planet == null || ws == S)
				{
					continue;
				}
				if ((object)ws.planet != (object)s.planet)
				{
					continue;
				}
				ws.ToStormFrame(camG, out double wsS, out _);
				if (Math.Abs(wsS) < distMe * 0.9)   
				{
					HideRest(A.fV, A.fC, 0, A.frontQuads);
					return;
				}
			}
		}
		catch
		{
		}
		double num = Math.Abs(s2) / s.Rmax;
		
		
		
		
		
		double num2;
		if (S.type == StormType.Typhoon)
		{
			num2 = Smooth01((num - 0.15) / 0.3) * Math.Exp(0.0 - WeatherSystem.Pow2(Math.Max(0.0, num - 1.6) / 2.2));
		}
		else
		{
			num2 = Smooth01((1.5 - num) / 1.2) * Math.Exp(0.0 - WeatherSystem.Pow2(num / 1.9));
		}
		num2 *= 1.0 - WeatherSystem.Clamp01((h - s.Hbase * 1.2) / (s.Hbase * 0.8));   
		num2 *= 0.55 + 0.45 * Math.Sin(num * 2.6 - s.age * 0.22);
		if (h < -200.0)
		{
			num2 = 0.0;
		}
		num2 = WeatherSystem.Clamp01(num2);
		float num3 = (float)TyphoonConfig.I.rainScale;
		
		
		
		if (num2 < 0.02 || (!farAbs && (double)camHalf > s.Rmax * 4.0))
		{
			HideRest(A.fV, A.fC, 0, A.frontQuads);
			return;
		}
		Double2 normalized = val.normalized;
		Double2 val3 = new Double2(0.0 - normalized.y, normalized.x);
		Double2 val4 = s.SampleWind(val);
		Vector2 val5 = new Vector2((float)Double2.Dot(val4, val3), (float)Double2.Dot(val4, normalized));
		float num4 = camHalf * 2.4f;
		float num5 = camHalf * 4.2f;
		float num6 = 9f + (float)s.Vmax * 0.05f;
		float num7 = camHalf * 0.16f * num3;
		float halfW = Mathf.Min(Mathf.Max(camHalf * 0.007f * num3, 0.06f), camHalf * 0.4f);
		if (farAbs)
		{
			
			
			
			float depth2;
			float halfCam2 = HalfExtent(cam, out depth2);
			float sR = (float)s.Rmax;
			num4 = sR * 2.4f;
			num5 = sR * 4.2f;
			num7 = sR * 0.16f * num3;
			halfW = Mathf.Min(Mathf.Max(sR * 0.007f * num3, sR * 0.002f), sR * 0.4f);
			if (num7 * (farAbsS / 10000f) < halfCam2 * 0.005f)
			{
				HideRest(A.fV, A.fC, 0, A.frontQuads);
				return;
			}
			
			num4 *= farAbsS / 10000f;
			num5 *= farAbsS / 10000f;
			num7 *= farAbsS / 10000f;
			halfW *= farAbsS / 10000f;
		}
		float num8 = (float)TyphoonConfig.I.rainOpacity * (float)num2 * (float)s.MergeFade();   
		
		
		
		
		
		
		
		
		Double2 stormC = FromStorm(0.0, 0.0);
		Double2 radialP = stormC.normalized;                       
		Double2 perpP = new Double2(0.0 - radialP.y, radialP.x);   
		Vector2 radialDir = new Vector2((float)radialP.x, (float)radialP.y);   
		Vector2 perp = new Vector2((float)perpP.x, (float)perpP.y);
		double fall = (s.age * 1.6) % 1.0;
		int dropsN = drops.Length;
		float rainW = Mathf.Max((float)(s.Rmax * 0.004) * num3 * rainWidScale, 0.03f);   
		float rainL = Mathf.Max((float)(s.Rmax * 0.09) * num3 * rainLenScale, 0.3f);     
		double lineW = s.Rmax * 1.6;
		
		
		
		double vH = Math.Abs(val5.x);
		double tiltRad = Math.Min(85.0, 5.0 * vH) * Math.PI / 180.0;
		Vector2 vertDir = radialDir;                                  
		Vector2 hzDir = perp * ((val5.x >= 0f) ? 1f : -1f);           
		Vector2 axisRain = (vertDir * (float)Math.Cos(tiltRad) + hzDir * (float)Math.Sin(tiltRad)).normalized;
		int num9 = 0;
		Color val8 = default(Color);
		for (int i = 0; i < dropsN; i++)
		{
			
			double phRand = ((double)((i * 2246822519u + 777u) % 1000) / 1000.0) * 0.15;
			
			
			
			
			double tt = ((double)i / (double)dropsN + fall + phRand) % 1.3;   
			
			double hh = (s.Hbase + (s.Htop - s.Hbase) * 0.15) * (1.0 - tt);
			
			double u = ((double)((i * 2654435761u) % 10000) / 10000.0);
			
			double offsetX = (Math.Pow(u, 1.5) * 2.0 - 1.0) * lineW * 0.5 + Math.Sin((double)i * 7.31 + s.age * 3.0) * lineW * 0.14;
			
			float born = Mathf.Clamp01((float)(tt / 0.1));
			float fade = 1f - (float)Smooth(WeatherSystem.Clamp01((tt - 1.0) / 0.3));
			float a = num8 * born * fade;
			if (a < 0.02f)
			{
				continue;
			}
			
			Double2 planetPos = stormC + radialP * hh + perpP * offsetX;
			Vector2 cen = WorldView.ToLocalPosition(planetPos);
			Vector2 qc = align ? (cen - alignOrigin) : cen;
			float hw = rainW;
			float hl = rainL;
			if (farAbs)
			{
				qc = (cen - alignOrigin) * (farAbsS / 10000f);
				hw = hw * farAbsS / 10000f;
				hl = hl * farAbsS / 10000f;
			}
			
			
			
			val8 = new Color(0.95f, 0.98f, 1f, Mathf.Min(1f, a * 1.15f));
			if (flashPower > 0.001f)
			{
				val8 = Color.Lerp(val8, new Color(1f, 1f, 0.95f, val8.a), flashPower * 0.7f);
			}
			if (num9 < A.frontQuads)
			{
				WriteQuad(A.fV, A.fT, A.fC, num9++, qc, hw, hl, axisRain, val8, 0f);   
			}
		}
		HideRest(A.fV, A.fC, num9, A.frontQuads);
	}

	private static void WriteQuad(Vector3[] v, Vector2[] t, Color[] c, int q, Vector2 centre, float halfW, float halfH, Vector2 axis, Color col, float uOffset)
	{
		int num = q * 4;
		Vector2 val = axis;
		Vector2 val2;
		if (!(val.sqrMagnitude > 0.0001f))
		{
			val2 = Vector2.up;
		}
		else
		{
			val = axis;
			val2 = val.normalized;
		}
		Vector2 val3 = val2;
		Vector2 val4 = new Vector2(val3.y, 0f - val3.x) * halfW;
		Vector2 val5 = val3 * halfH;
		v[num] = new Vector3(centre.x - val4.x - val5.x, centre.y - val4.y - val5.y, 0f);
		v[num + 1] = new Vector3(centre.x - val4.x + val5.x, centre.y - val4.y + val5.y, 0f);
		v[num + 2] = new Vector3(centre.x + val4.x + val5.x, centre.y + val4.y + val5.y, 0f);
		v[num + 3] = new Vector3(centre.x + val4.x - val5.x, centre.y + val4.y - val5.y, 0f);
		float num2 = uOffset + 0.5f;
		t[num] = new Vector2(uOffset, 0f);
		t[num + 1] = new Vector2(uOffset, 1f);
		t[num + 2] = new Vector2(num2, 1f);
		t[num + 3] = new Vector2(num2, 0f);
		c[num] = col;
		c[num + 1] = col;
		c[num + 2] = col;
		c[num + 3] = col;
	}

	private static void HideRest(Vector3[] v, Color[] c, int from, int total)
	{
		for (int i = from; i < total; i++)
		{
			int num = i * 4;
			v[num] = Vector3.zero;
			v[num + 1] = Vector3.zero;
			v[num + 2] = Vector3.zero;
			v[num + 3] = Vector3.zero;
			c[num] = Color.clear;
			c[num + 1] = Color.clear;
			c[num + 2] = Color.clear;
			c[num + 3] = Color.clear;
		}
	}

	private static double Smooth01(double x)
	{
		if (x <= 0.0)
		{
			return 0.0;
		}
		if (x >= 1.0)
		{
			return 1.0;
		}
		return x * x * (3.0 - 2.0 * x);
	}
}
