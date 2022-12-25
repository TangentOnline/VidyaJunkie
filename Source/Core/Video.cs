using System.Buffers;
using System.Text;
using System.Text.Json.Serialization;
using CommunityToolkit.HighPerformance;
using QoiSharp.Codec;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xabe.FFmpeg;

namespace VidyaJunkie;

public class Video : IEquatable<Video>, SwapListElement {
	public string videoName = "";

	[JsonIgnore]
	private string _videoUrl = "";

	public string videoUrl {
		get => this._videoUrl;
		set {
			this._videoUrl = value;
			this.videoUrlHash = this._videoUrl.GetDeterministicHashCode();
		}
	}

	[JsonIgnore]
	public int videoUrlHash;

	public TimeSpan videoLength = TimeSpan.Zero;
	public DateTime videoDateUploaded = DateTime.UnixEpoch;
	public DateTime videoDateAdded = DateTime.Now;

	public string videoThumbnailUrl = "";
	public string videoThumbnailFilename = "";
	[JsonIgnore]
	public string VideoThumbnailFilepath => Path.Combine(this.inPlaylist.ThumbnailsPath, this.videoThumbnailFilename);

	public string uploaderName = "";
	public string uploaderUrl = "";
	public string uploaderDomain = "";

	[JsonIgnore]
	public float fuzzySimilarityScore = 0;

	[JsonIgnore]
	public Playlist inPlaylist;

	[JsonIgnore]
	public int listIndex { get; set; }

	public Task CreateThumbnail() {
		if (this.videoThumbnailUrl != "" && !this.HasThumbnail()) {
			return Task.Run(() => {
				try {
					StringBuilder sb = new StringBuilder($"{this.videoName}_{this.videoUrlHash}");
					foreach (char invalidFileNameChar in Path.GetInvalidFileNameChars()) {
						sb.Replace(invalidFileNameChar.ToString(), "");
					}
					string localFilename = sb.ToString();

					if (AddWindow.RawVideoExtensions.Any(e => this.videoThumbnailUrl.EndsWith(e, StringComparison.CurrentCultureIgnoreCase))) {
						localFilename += ".png";
						string path = Path.Combine(this.inPlaylist.ThumbnailsPath, localFilename);
						if (!File.Exists(path)) {
							IConversion conversion = FFmpeg.Conversions.FromSnippet.Snapshot(this.videoThumbnailUrl, Path.Combine(this.inPlaylist.ThumbnailsPath, localFilename), TimeSpan.FromSeconds(0.5)).Result;
							conversion.AddParameter("-vf scale=256:-1");
							IConversionResult result = conversion.Start().Result;
						}

						this.videoThumbnailFilename = localFilename;
						return;
					}

					localFilename += ".qoi";

					byte[] imageBytes = Program.HttpClient.GetByteArrayAsync(this.videoThumbnailUrl).Result;
					using (Image<Bgra32> image = Image.Load<Bgra32>(new Configuration { PreferContiguousImageBuffers = true }, imageBytes, TextureUtilities.GetDecoder(Image.DetectFormat(imageBytes).FileExtensions.ToList()))) {
						if (image.Width > 256) {
							image.Mutate(i => i.Resize(new Size(256, 0)));
						}

						if (!image.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> memory)) {
							throw new Exception("This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");
						}

						using (MemoryHandle pinHandle = memory.Pin()) {
							byte[] qoiBytes = TextureUtilities.EncodeQoi(memory.AsBytes().Span, image.Width, image.Height, Channels.RgbWithAlpha, ColorSpace.Linear);
							File.WriteAllBytes(Path.Combine(this.inPlaylist.ThumbnailsPath, localFilename), qoiBytes);
							this.videoThumbnailFilename = localFilename;
						}
					}
				} catch (Exception e) { }
			});
		}

		return Task.CompletedTask;
	}

	public bool HasThumbnail() {
		return this.videoThumbnailFilename != "" && File.Exists(this.VideoThumbnailFilepath);
	}

	public Texture GetThumbnailTexture() {
		if (this.HasThumbnail()) {
			return Texture.CacheLoad(this.VideoThumbnailFilepath);
		}

		return Texture.PlaceholderTexture;
	}

	public Video Clone() {
		return new Video {
			videoName = new string(this.videoName),
			videoUrl = new string(this.videoUrl),
			uploaderName = new string(this.uploaderName),
			uploaderUrl = new string(this.uploaderUrl),
			videoLength = this.videoLength,
			videoDateUploaded = this.videoDateUploaded,
			videoDateAdded = this.videoDateAdded,
			uploaderDomain = new string(this.uploaderDomain),
			videoThumbnailUrl = new string(this.videoThumbnailUrl),
			videoUrlHash = this.videoUrlHash,
			videoThumbnailFilename = new string(this.videoThumbnailFilename),
			inPlaylist = this.inPlaylist
		};
	}

	public bool Equals(Video other) {
		return this.videoUrlHash == other.videoUrlHash && this.videoUrl == other.videoUrl;
	}

	public override int GetHashCode() {
		return this.videoUrlHash;
	}

	public override bool Equals(object obj) {
		return this.Equals(obj as Video);
	}

	public bool IsValid() {
		return this.videoName != "" && this.videoUrl != "";
	}
}

[JsonSerializable(typeof(Video))]
[JsonSerializable(typeof(List<Video>))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default, IncludeFields = true, WriteIndented = true)]
public partial class VideoJsonContext : JsonSerializerContext { }
