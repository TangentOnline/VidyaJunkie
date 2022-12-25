using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace VidyaJunkie;

public static class Settings {
	private static SettingsData Data = new SettingsData();
	private static string Filepath => Path.Combine(Resource.ResourceFolder, "Settings.json");

	static Settings() {
		if (!File.Exists(Filepath)) {
			using (FileStream fileStream = File.Create(Filepath)) {
				fileStream.Write("{}"u8);
			}
		}

		Load();
		AppDomain.CurrentDomain.ProcessExit += (s, e) => { Save(); };
	}

	public static void Save() {
		using (FileStream file = File.OpenWrite(Filepath)) {
			Utf8JsonWriter writer = new Utf8JsonWriter(file, new JsonWriterOptions {
				Indented = true,
				SkipValidation = false
			});
			JsonSerializer.Serialize(writer, Data, SettingsDataJsonContext.Default.SettingsData);
		}
	}

	public static void Load() {
		Utf8JsonReader reader = new Utf8JsonReader(File.ReadAllBytes(Filepath), true, new JsonReaderState());
		Data = JsonSerializer.Deserialize(ref reader, SettingsDataJsonContext.Default.SettingsData) ?? new SettingsData();
	}

	public static Vector2D<int> WindowSize {
		get => Data.windowSize;
		set => Data.windowSize = value;
	}

	public static Vector2D<int> WindowPosition {
		get => Data.windowPosition;
		set => Data.windowPosition = value;
	}

	public static bool WindowMaximised {
		get => Data.windowMaximised;
		set => Data.windowMaximised = value;
	}

	public static float GuiScale {
		get => Data.guiScale;
		set => Data.guiScale = value;
	}

	public static int FontSize {
		get => Data.fontSize;
		set => Data.fontSize = value;
	}

	public static float FuzzySearchSensitivity {
		get => Data.fuzzySearchSensitivity;
		set => Data.fuzzySearchSensitivity = value;
	}
}

public class SettingsData {
	public SettingsData() {
		IMonitor monitor = null;
		try {
			monitor = Silk.NET.Windowing.Monitor.GetMainMonitor(null);
		} catch (Exception e) { }

		if (monitor != null & monitor.VideoMode.Resolution != null) {
			this.windowPosition = monitor.VideoMode.Resolution.Value / 4;
			this.windowSize = monitor.VideoMode.Resolution.Value / 2;
		} else {
			this.windowPosition = new Vector2D<int>(0, 0);
			this.windowSize = new Vector2D<int>(1280, 720);
		}
	}

	public float guiScale = 0.5f;
	public int fontSize = 42;
	public float fuzzySearchSensitivity = 0.95f;

	public Vector2D<int> windowPosition;
	public Vector2D<int> windowSize;
	public bool windowMaximised;
}

[JsonSerializable(typeof(SettingsData))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default, IncludeFields = true, WriteIndented = true)]
public partial class SettingsDataJsonContext : JsonSerializerContext { }
