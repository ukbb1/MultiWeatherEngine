using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace MultiWeatherEngine;

public class TyphoonConfig
{
	public static TyphoonConfig I = new TyphoonConfig();

	private static string path;

	public double eyewallRadiusAsPlanetFraction = 0.005;

	public double eyewallRadiusMinMeters = 2500.0;

	public double eyewallRadiusMaxMeters = 60000.0;

	public double outerRadiusMultiplier = 9.0;

	
	
	
	
	public double topHeightMinMeters = 4500.0;

	public double topHeightMidMeters = 6000.0;

	public double topHeightMaxMeters = 9000.0;

	public double updraftFraction = 0.32;

	public double outflowFraction = 0.75;

	public double gustAmplitude = 0.35;

	public double driftSpeed = 9.0;

	public double spawnLeadDistanceMeters = 60000.0;

	public bool cameraShake = true;

	public double cameraShakeScale = 1.0;

	public bool affectAstronauts = true;

	public bool visuals = true;

	public int canopyPuffs = 1500;

	public int cloudPuffs = 1100;

	public int rainDrops = 1700;

	public double cloudOpacity = 0.95;

	public double rainOpacity = 0.85;

	public double rainScale = 1.0;

	public double skyOpacity = 0.78;

	public bool lightning = true;

	public bool hud = true;

	public static void Load(string modFolder)
	{
		if (string.IsNullOrEmpty(modFolder))
		{
			return;
		}
		path = Path.Combine(modFolder, "typhoon_config.json");
		try
		{
			if (File.Exists(path))
			{
				TyphoonConfig typhoonConfig = JsonConvert.DeserializeObject<TyphoonConfig>(File.ReadAllText(path));
				if (typhoonConfig != null)
				{
					
					typhoonConfig.topHeightMinMeters = 4500.0;
					typhoonConfig.topHeightMidMeters = 6000.0;
					typhoonConfig.topHeightMaxMeters = 9000.0;
					I = typhoonConfig;
				}
			}
			File.WriteAllText(path, JsonConvert.SerializeObject((object)I, (Formatting)1));
		}
		catch (Exception ex)
		{
			Debug.LogWarning((object)("[Typhoon] config: " + ex.Message));
		}
	}
}
