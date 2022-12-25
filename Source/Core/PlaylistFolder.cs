using System.Diagnostics;

namespace VidyaJunkie;

public class PlaylistFolder {
	public string Name { get; private set; }
	public string Folderpath => this.GetFolderpath();

	private PlaylistFolder? parentFolder;

	private List<Playlist> playlists;
	private List<PlaylistFolder> folders;

	private Stopwatch timer;

	private static float AutoSaveInterval = 30.0f;
	private float randomSaveInterval;

	public PlaylistFolder(string name, PlaylistFolder parent) {
		this.Name = name;
		this.parentFolder = parent;

		if (!Directory.Exists(this.Folderpath)) {
			Directory.CreateDirectory(this.Folderpath);
		}

		this.timer = Stopwatch.StartNew();
		this.randomSaveInterval = (Random.Shared.NextSingle() * AutoSaveInterval);
		this.LoadAll();
	}

	public string GetFolderpath() {
		if (this.parentFolder == null) {
			return Resource.GetResourceFolderPath(this.Name);
		}

		return Path.Combine(this.parentFolder.GetFolderpath(), this.Name);
	}

	public void Update() {
		if ((this.timer.Elapsed.TotalSeconds - this.randomSaveInterval) >= (AutoSaveInterval - this.randomSaveInterval)) {
			foreach (Playlist playlist in this.GetAllPlaylists())
				playlist.Save();
			this.timer.Restart();
		}

		foreach (PlaylistFolder playlistFolder in this.folders)
			playlistFolder.Update();
	}

	public void CreatePlaylist(string name) {
		if (name.Length < 1 || name.StartsWith('.') || name.Any(c => Path.GetInvalidFileNameChars().Contains(c))) {
			return;
		}

		string filepath = Path.Combine(this.Folderpath, $"{name}.json");
		if (File.Exists(filepath)) {
			return;
		}

		this.AddPlaylist(new Playlist(name, this));
	}

	public void AddPlaylist(Playlist playlist) {
		this.playlists.Add(playlist);
	}

	public void RemovePlaylistOnDisk(Playlist playlist) {
		if (this.playlists.Contains(playlist)) {
			Utilities.DeleteFileToRecycleBin(playlist.Filepath);
			Utilities.DeleteFolderToRecycleBin(playlist.ThumbnailsPath);
			this.RemovePlaylist(playlist);
		}
	}

	public void RemovePlaylist(Playlist playlist) {
		this.playlists.Remove(playlist);
	}

	public void CreateFolder(string name) {
		if (name.Length < 1 || name.StartsWith(".")) {
			return;
		}

		string folderpath = Path.Combine(this.Folderpath, name);
		if (Directory.Exists(folderpath)) {
			return;
		}

		this.AddFolder(new PlaylistFolder(name, this));
	}

	private void AddFolder(PlaylistFolder playlistFolder) {
		playlistFolder.parentFolder = this;
		this.folders.Add(playlistFolder);
	}

	public void RemoveFolderOnDisk(PlaylistFolder playlistFolder) {
		if (this.folders.Contains(playlistFolder)) {
			Utilities.DeleteFolderToRecycleBin(playlistFolder.Folderpath);
			this.RemoveFolder(playlistFolder);
		}
	}

	public void RemoveFolder(PlaylistFolder playlistFolder) {
		this.folders.Remove(playlistFolder);
	}

	public void Rename(string newName) {
		if (newName.Length < 1 || newName.StartsWith('.') || this.Name == newName || newName.Any(c => Path.GetInvalidFileNameChars().Contains(c))) {
			return;
		}

		string newFolderpath = $"{this.Folderpath.Substring(0, this.Folderpath.LastIndexOf(this.Name))}{newName}";
		if (!Path.Exists(newFolderpath)) {
			Directory.Move(this.Folderpath, newFolderpath);
			this.Name = newName;
			this.LoadAll();
		} else if (string.Equals(this.Name, newName, StringComparison.CurrentCultureIgnoreCase)) {
			Directory.Move(this.Folderpath, newFolderpath);
			this.Name = newName;
		}
	}

	public void Move(PlaylistFolder playlistFolder) {
		if (this == playlistFolder || this.parentFolder == playlistFolder || playlistFolder.Folderpath.Contains(this.Folderpath)) {
			return;
		}

		this.parentFolder.RemoveFolder(this);
		Directory.Move(this.Folderpath, Path.Combine(playlistFolder.Folderpath, this.Name));
		playlistFolder.AddFolder(this);
		this.parentFolder = playlistFolder;
	}

	public void SaveAll() {
		foreach (Playlist playlist in this.playlists)
			playlist.Save();
		foreach (PlaylistFolder playlistFolder in this.folders)
			playlistFolder.SaveAll();
	}

	public void AwaitSaveAll() {
		foreach (Playlist playlist in this.playlists)
			playlist.AwaitSave();
		foreach (PlaylistFolder playlistFolder in this.folders)
			playlistFolder.AwaitSaveAll();
	}

	private void LoadAll() {
		this.playlists = new List<Playlist>();
		this.folders = new List<PlaylistFolder>();

		foreach (string file in Directory.EnumerateFiles(this.Folderpath))
			if (file.EndsWith(".json")) {
				this.AddPlaylist(new Playlist(Path.GetFileNameWithoutExtension(file), this));
			}

		foreach (string folder in Directory.EnumerateDirectories(this.Folderpath))
			if (!Path.GetFileName(folder).StartsWith(".")) {
				this.AddFolder(new PlaylistFolder(Path.GetFileName(folder), this));
			}
	}

	public void ReloadAll() {
		foreach (Playlist playlist in this.playlists)
			playlist.Load();
		foreach (PlaylistFolder playlistFolder in this.folders)
			playlistFolder.ReloadAll();
	}

	public List<Playlist> GetAllPlaylists() {
		return this.playlists;
	}

	public void GetAllPlaylistsRecurse(List<Playlist> playlists) {
		playlists.AddRange(this.playlists);
		foreach (PlaylistFolder playlistFolder in this.folders)
			playlistFolder.GetAllPlaylistsRecurse(playlists);
	}

	public List<PlaylistFolder> GetAllPlaylistFolders() {
		return this.folders;
	}

	public void GetAllPlaylistFoldersRecurse(List<PlaylistFolder> folders) {
		folders.AddRange(this.folders);
		foreach (PlaylistFolder playlistFolder in this.folders)
			playlistFolder.GetAllPlaylistFoldersRecurse(folders);
	}

	public List<Video> GetAllVideos(bool distinct = true) {
		List<Video> videos = new List<Video>();

		foreach (Playlist playlist in this.playlists)
			videos.AddRange(playlist.GetAllVideos());

		if (distinct) {
			return videos.Distinct().ToList();
		}

		return videos;
	}

	public void GetAllVideosRecurse(ref List<Video> videos, bool distinct = true) {
		this.GetAllVideosRecurse(videos, 0);
		if (distinct) {
			videos = videos.Distinct().ToList();
		}
	}

	private void GetAllVideosRecurse(List<Video> videos, int depth) {
		foreach (PlaylistFolder playlistFolder in this.folders)
			playlistFolder.GetAllVideosRecurse(videos, depth + 1);
		foreach (Playlist playlist in this.playlists)
			videos.AddRange(playlist.GetAllVideos());
	}

	public int GetVideoCountRecurse() {
		int count = 0;

		foreach (PlaylistFolder playlistFolder in this.folders)
			count += playlistFolder.GetVideoCountRecurse();
		foreach (Playlist playlist in this.playlists)
			count += playlist.GetVideoCount();

		return count;
	}

	public int GetVideoCount() {
		int count = 0;

		foreach (Playlist playlist in this.playlists)
			count += playlist.GetVideoCount();

		return count;
	}

	public int GetPlaylistCount() {
		return this.playlists.Count;
	}

	public int GetPlaylistCountRecurse() {
		int count = 0;

		foreach (PlaylistFolder playlistFolder in this.folders)
			count += playlistFolder.GetPlaylistCountRecurse();

		count += this.GetPlaylistCount();

		return count;
	}
}
