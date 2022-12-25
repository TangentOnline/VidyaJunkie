using System.Buffers;
using QoiSharp;
using Silk.NET.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace VidyaJunkie;

public class Texture : IDisposable {
	public int Width { get; private set; }
	public int Height { get; private set; }
	public bool VramLoaded { get; private set; }

	private Image<Bgra32>? sixLaborsImage;
	private QoiImage? qoiImage;
	private uint glHandle;

	private static int MaxLoadedTextures = 200;
	private static Queue<string> LoadedTextures = new Queue<string>(MaxLoadedTextures);
	private static Dictionary<string, Texture> TextureCache = new Dictionary<string, Texture>();

	public static Texture PlaceholderTexture => PlaceholderTextureLoader.Result;
	private static Task<Texture> PlaceholderTextureLoader;

	static Texture() {
		PlaceholderTextureLoader = Task.Run(() => {
			return new Texture(Resource.GetResourceFilePath("Domain Icons/V4C.png"));
		});
	}

	public Texture(string filepath) {
		if (Path.GetExtension(filepath) == ".qoi") {
			this.LoadQoi(filepath);
		} else {
			using (FileStream stream = File.OpenRead(filepath)) {
				this.LoadSixLabors(stream);
			}
		}
	}

	public Texture(Uri link) {
		using (Stream stream = Program.HttpClient.GetStreamAsync(link).Result) {
			this.LoadSixLabors(stream);
		}
	}

	public Texture(Stream stream) {
		this.LoadSixLabors(stream);
	}

	public Texture(Image<Bgra32> image) {
		this.sixLaborsImage = image;
		this.Width = this.sixLaborsImage.Width;
		this.Height = this.sixLaborsImage.Height;
	}

	private void LoadQoi(string filepath) {
		this.qoiImage = QoiDecoder.Decode(File.ReadAllBytes(filepath));
		this.Width = this.qoiImage.Width;
		this.Height = this.qoiImage.Height;
	}

	private void LoadSixLabors(Stream stream) {
		Configuration config = new Configuration {
			PreferContiguousImageBuffers = true
		};
		List<string> extensions = Image.DetectFormat(stream).FileExtensions.ToList();

		this.sixLaborsImage = Image.Load<Bgra32>(config, stream, TextureUtilities.GetDecoder(extensions));
		this.Width = this.sixLaborsImage.Width;
		this.Height = this.sixLaborsImage.Height;
	}

	public unsafe void LoadVRAM() {
		this.glHandle = Program.GlContext.GenTexture();
		this.Bind();

		if (this.sixLaborsImage != null) {
			if (!this.sixLaborsImage.DangerousTryGetSinglePixelMemory(out Memory<Bgra32> memory)) {
				throw new Exception("This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");
			}

			using (MemoryHandle mm = memory.Pin()) {
				void* ptr = mm.Pointer;
				Program.GlContext.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)this.Width, (uint)this.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, ptr);
			}
		} else if (this.qoiImage != null) {
			fixed (void* ptr = this.qoiImage.Data) {
				Program.GlContext.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)this.Width, (uint)this.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, ptr);
			}
		}

		this.SetParameters();
		this.VramLoaded = true;
		this.UnloadRAM();
	}

	private void UnloadRAM() {
		this.qoiImage = null;
		this.sixLaborsImage = null;
	}

	private void SetParameters() {
		Program.GlContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.Repeat);
		Program.GlContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.Repeat);
		Program.GlContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.Nearest);
		Program.GlContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
		//Program.GlContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
		//Program.GlContext.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 8);
		//Program.GlContext.GenerateMipmap(TextureTarget.Texture2D);
	}

	public void Bind(TextureUnit textureSlot = TextureUnit.Texture0) {
		Program.GlContext.ActiveTexture(textureSlot);
		Program.GlContext.BindTexture(TextureTarget.Texture2D, this.glHandle);
	}

	public void Dispose() {
		Program.GlContext.DeleteTexture(this.glHandle);
	}

	public nint GetIntPtr() {
		return new nint(this.glHandle);
	}

	public static Texture CacheLoad(string filepath) {
		if (!TextureCache.ContainsKey(filepath)) {
			TextureCache[filepath] = PlaceholderTexture;
			Task.Run(() => {
				TextureCache[filepath] = new Texture(filepath);
			});
		}

		Texture texture = TextureCache[filepath];
		if (!texture.VramLoaded) {
			texture.LoadVRAM();
			LoadedTextures.Enqueue(filepath);
			while (LoadedTextures.Count > MaxLoadedTextures) {
				TextureCache.Remove(LoadedTextures.Dequeue());
			}
		}

		return texture;
	}

	public static Texture CacheLoad(Uri uri) {
		if (!TextureCache.ContainsKey(uri.OriginalString)) {
			TextureCache[uri.OriginalString] = PlaceholderTexture;
			Task.Run(() => {
				TextureCache[uri.OriginalString] = new Texture(uri);
			});
		}

		Texture texture = TextureCache[uri.OriginalString];
		if (!texture.VramLoaded) {
			texture.LoadVRAM();
			LoadedTextures.Enqueue(uri.OriginalString);
			while (LoadedTextures.Count > MaxLoadedTextures) {
				TextureCache.Remove(LoadedTextures.Dequeue());
			}
		}

		return texture;
	}

	public static Texture GetDomainTexture(string domainName) {
		if (domainName != "") {
			switch (domainName) {
				case "Youtube":
					return CacheLoad(Resource.GetResourceFilePath("Domain Icons/Youtube.png"));
				case "Vimeo":
					return CacheLoad(Resource.GetResourceFilePath("Domain Icons/Vimeo.png"));
				case "Dailymotion":
					return CacheLoad(Resource.GetResourceFilePath("Domain Icons/Dailymotion.png"));
				case "Streamable":
					return CacheLoad(Resource.GetResourceFilePath("Domain Icons/Streamable.png"));
				case "NicoVideo":
					return CacheLoad(Resource.GetResourceFilePath("Domain Icons/NicoVideo.png"));
				case "Discord":
					return CacheLoad(Resource.GetResourceFilePath("Domain Icons/Discord.png"));
			}
		}

		return PlaceholderTexture;
	}
}