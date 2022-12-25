using System.Text.Json;

namespace VidyaJunkie;

public class Playlist {
	public string Name { get; private set; } = "";

	public string Filepath => Path.Combine(this.parentFolder.GetFolderpath(), $"{this.Name}.json");
	public string ThumbnailsPath => Path.Combine(this.parentFolder.GetFolderpath(), $".{this.Name} Thumbnails");

	private PlaylistFolder parentFolder;
	private List<Video> videos = new List<Video>();
	private bool unsavedChanges;
	private Task? saveTask = null;

	public Playlist(string name, PlaylistFolder parentFolder) {
		this.Name = name;
		this.parentFolder = parentFolder;
		if (File.Exists(this.Filepath)) {
			this.Load();
		} else {
			using (FileStream fileStream = File.Create(this.Filepath)) {
				fileStream.Write("[]"u8);
			}

			Directory.CreateDirectory(this.ThumbnailsPath);
		}
	}

	public void Load() {
		Task.Run(() => {
			List<Video> localVids = new List<Video>();
			Utf8JsonReader reader = new Utf8JsonReader(File.ReadAllBytes(this.Filepath), true, new JsonReaderState());
			localVids = JsonSerializer.Deserialize(ref reader, VideoJsonContext.Default.ListVideo) ?? new List<Video>();
			localVids.SwapInit();
			for (int index = 0; index < localVids.Count; index++) {
				localVids[index].inPlaylist = this;
			}

			this.videos = localVids;
		});
	}

	public void Save() {
		if (this.unsavedChanges) {
			this.unsavedChanges = false;
			this.saveTask = Task.Run(() => {
				lock (this.Name) {
					try {
						using (FileStream file = File.OpenWrite(this.Filepath)) {
							Utf8JsonWriter writer = new Utf8JsonWriter(file, new JsonWriterOptions {
								Indented = true,
								SkipValidation = false,
							});
							JsonSerializer.Serialize(writer, this.videos, VideoJsonContext.Default.ListVideo);
						}
					} catch (Exception e) {
						this.unsavedChanges = true;
					}
				}
			});
		}
	}

	public void AwaitSave() {
		if (this.saveTask != null) {
			this.saveTask.Wait();
		}
	}

	public void Rename(string newName) {
		if (newName.Length < 1 || this.Name == newName || newName.Any(c => Path.GetInvalidFileNameChars().Contains(c))) {
			return;
		}

		string oldPath = this.Filepath;
		string oldThumbnailsPath = this.ThumbnailsPath;
		this.Name = newName;
		string newPath = this.Filepath;
		string newThumbnailsPath = this.ThumbnailsPath;
		File.Move(oldPath, newPath, true);
		Directory.Move(oldThumbnailsPath, newThumbnailsPath);
	}

	public void Move(PlaylistFolder newPlaylistFolder) {
		if (this.parentFolder == newPlaylistFolder) {
			return;
		}

		PlaylistFolder oldPlaylistFolder = this.parentFolder;
		string oldPath = this.Filepath;
		string newPath = Path.Combine(newPlaylistFolder.GetFolderpath(), $"{this.Name}.json");
		if (!File.Exists(newPath)) {
			oldPlaylistFolder.RemovePlaylist(this);
			newPlaylistFolder.AddPlaylist(this);
			File.Move(oldPath, newPath);
			Directory.Move(this.ThumbnailsPath, Path.Combine(newPlaylistFolder.GetFolderpath(), $".{this.Name} Thumbnails"));
			this.parentFolder = newPlaylistFolder;
		}
	}

	public bool ContainsVideo(Video video) {
		if (video.listIndex < this.videos.Count && this.videos[video.listIndex].Equals(video)) {
			return true;
		}

		for (int i = 0; i < this.videos.Count; i++) {
			if (this.videos[i].Equals(video)) {
				return true;
			}
		}

		return false;
	}

	public void AddVideo(Video video) {
		if (video.IsValid() && !this.ContainsVideo(video)) {
			video.inPlaylist = this;
			this.videos.SwapAddFast(video);
			this.unsavedChanges = true;
		}
	}

	public void RemoveVideo(Video video) {
		Task.Run(() => {
			if (video.HasThumbnail()) {
				File.Delete(video.VideoThumbnailFilepath);
			}
		});
		this.videos.SwapRemoveFast(video);
		this.unsavedChanges = true;
	}

	public void RemoveVideo(string videoUrl) {
		int videoHash = videoUrl.GetDeterministicHashCode();

		for (int i = this.videos.Count - 1; i >= 0; i--) {
			Video video = this.videos[i];
			if (video.videoUrlHash == videoHash && video.videoUrl == videoUrl) {
				this.RemoveVideo(video);
				return;
			}
		}
	}

	public List<Video> GetAllVideos() {
		return this.videos;
	}

	public int GetVideoCount() {
		return this.videos.Count;
	}
}
