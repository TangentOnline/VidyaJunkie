namespace VidyaJunkie;

public static class Resource {
	public static readonly string ResourceFolder;
	public static readonly string TempFolder;
	private static Dictionary<string, string> Cache = new Dictionary<string, string>();

	static Resource() {
		ResourceFolder = $@"{Environment.CurrentDirectory}\Resource";
		TempFolder = $@"{Environment.CurrentDirectory}\Resource\Temp";
		if (!Directory.Exists(TempFolder)) {
			Directory.CreateDirectory(TempFolder);
		}
	}

	public static string GetResourceFilePath(string filename) {
		if (Cache.TryGetValue(filename, out string path)) {
			return path;
		}

		path = Path.Combine(ResourceFolder, filename);
		if (!File.Exists(path)) {
			throw new Exception($"File Doesnt Exist: {path}");
		}

		Cache.Add(filename, path);

		return path;
	}

	public static string GetResourceFolderPath(string foldername) {
		if (Cache.TryGetValue(foldername, out string path)) {
			return path;
		}

		path = Path.Combine(ResourceFolder, foldername);
		if (!Directory.Exists(path)) {
			throw new Exception($"Folder Doesnt Exist: {path}");
		}

		Cache.Add(foldername, path);

		return path;
	}
}
