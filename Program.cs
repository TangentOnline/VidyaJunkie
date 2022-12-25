using System.Drawing;
using System.Net;
using System.Numerics;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;
using VideoMode = Silk.NET.Windowing.VideoMode;

namespace VidyaJunkie;

public static class Program {
	public static IWindow MainWindow;
	public static ImGuiController ImGuiController;
	public static GL GlContext;
	public static IInputContext InputContext;

	public static PlaylistFolder MainPlaylistFolder;

	public static PlaylistWindow PlaylistWindow = new PlaylistWindow();
	public static AddWindow AddWindow = new AddWindow();
	public static SearchWindow SearchWindow = new SearchWindow();
	public static SettingsWindow SettingsWindow = new SettingsWindow();

	public static HttpClient HttpClient = new HttpClient();
	public static bool FFMPEGLoaded;

	public static unsafe void Main(string[] args) {
		Utilities.LogExceptions();
		if (!Utilities.IsProcessUnique()) {
			return;
		}

		ServicePointManager.DefaultConnectionLimit = 25;

		if (!File.Exists(Path.Combine(Resource.ResourceFolder, "VideoPlayer.html"))) {
			File.Create(Path.Combine(Resource.ResourceFolder, "VideoPlayer.html"));
		}

		if (!File.Exists(Path.Combine(Resource.ResourceFolder, "Exceptions.txt"))) {
			File.Create(Path.Combine(Resource.ResourceFolder, "Exceptions.txt"));
		}

		if (!Directory.Exists(Path.Combine(Resource.ResourceFolder, "FFMPEG"))) {
			Directory.CreateDirectory(Path.Combine(Resource.ResourceFolder, "FFMPEG"));
		}

		if (!Directory.Exists(Path.Combine(Resource.ResourceFolder, "Playlists"))) {
			Directory.CreateDirectory(Path.Combine(Resource.ResourceFolder, "Playlists"));
		}

		Task.Run(() => {
			FFmpeg.SetExecutablesPath(Resource.GetResourceFolderPath("FFMPEG"));
			FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, Resource.GetResourceFolderPath("FFMPEG")).Wait();
			FFMPEGLoaded = true;
		});

		Task mainPlaylistLoader = Task.Run(() => {
			MainPlaylistFolder = new PlaylistFolder("Playlists", null);
		});

		WindowOptions windowOptions = new WindowOptions(false, Settings.WindowPosition, Settings.WindowSize, 0, 0, GraphicsAPI.Default, "Vidya Junkie", WindowState.Normal, WindowBorder.Resizable, true, true, VideoMode.Default);
		windowOptions.WindowState = Settings.WindowMaximised ? WindowState.Maximized : WindowState.Normal;
		try {
			Window.PrioritizeGlfw();
			MainWindow = Window.Create(windowOptions);
		} catch (Exception e) {
			Window.PrioritizeSdl();
			MainWindow = Window.Create(windowOptions);
		}
		MainWindow.IsVisible = false;
		bool renderOnce = false;

		MainWindow.FramebufferResize += size => {
			GlContext.Viewport(size);
		};
		MainWindow.StateChanged += state => {
			if (state == WindowState.Maximized) {
				Settings.WindowMaximised = true;
			} else if (state == WindowState.Normal) {
				Settings.WindowMaximised = false;
			}
		};
		MainWindow.Move += pos => {
			Settings.WindowPosition = pos;
		};
		MainWindow.Resize += size => {
			Settings.WindowSize = size;
		};

		MainWindow.Load += () => {
			GlContext = MainWindow.CreateOpenGL();
			GlContext.ClearColor(Color.FromArgb(255, 0, 0, 0));

			InputContext = MainWindow.CreateInput();

			ImGuiController = new ImGuiController(
				GlContext,
				MainWindow,
				InputContext,
				() => {
					ImFontConfig* fontConfig = ImGuiNative.ImFontConfig_ImFontConfig();
					fontConfig->OversampleH = 1;
					fontConfig->OversampleV = 1;
					ImGui.GetIO().Fonts.Flags |= ImFontAtlasFlags.NoPowerOfTwoHeight;

					ImGui.GetIO().Fonts.AddFontFromFileTTF(Resource.GetResourceFilePath("Fonts/NotoSans-Regular.ttf"), Settings.FontSize, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesDefault());

					fontConfig->MergeMode = 1;

					ImFontGlyphRangesBuilderPtr symbolBuilder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
					symbolBuilder.AddText("♫∇O╭╮Oƪ☆▽☆%∀◍◎●◐◑◒◓◔◕◖◗❂☢⊗⊙◘◙⊚⊘⊖⊕∘∅◦⬤⚫〶〇◯◡◠◟◞◝◜◛◚⊛⊜⊝❍⦿♩♪♫♬♭♮♯°ø؂≠≭【】「」『』〈〉《》︻︼（）＜＞｛｝〖〗◌○◉★☆✡✦✧✩✪✫✬✭✮✯✰⁂⁎⁑✢✣✤✥✱✲✳✴✵✶✷✸✹✺✻✼✽✿❀❁❂❃❇❈❉❊❋❄❆❅⋆≛ᕯ✲࿏꙰۞⭒⍟©®ΧΨΩΦΥΤΣΡΠΟΞΝΜΛΚΙΘΗΖΕΔΓΒΑωψχχφυτσςπρποξνμλκιθηζεδγβα＆％～¿√Σ∞™℠℡℗‱¢$€£¥₮৲৳௹฿៛₠₡₢₣₤₥₦₧₨₩₪₫₭₯₰₱₲₳₴₵￥﷼¤ƒ°℃℉♀♂＠＄＃π АБВГДЕЁЖЗИЙКЛМНОабвгдеёжзийклмноПРСТУФХЦЧШЩЪЫЬЭЮЯпрстуфхцчшщъыьэюя«»„“—…́ІѢѲѴіѣѳѵ ÅÄÖåäö ÁÉÍÑÓÚÜáéíñóúü¿¡«»“”‘’—–… ÅÆØøæå āēīōūȳÆŒăĕĭŏŭy̆æœ ÀàéÉèÈìÌîÎóÓòÒùÙ…–—’‘”“»« ÁÉĒḖÍÏḮÓÚÜǗŌṒáéēḗíïḯóúüǘōṓÀÃÈḔẼÌĨÒÙŨṐÕàãèḕẽìĩòùũṑõăāĭīŭū̀́̃̓̈ͅ ΑΆΒΓΔΕΈΖΗΉΘΙΊΚΛαάβγδεέζηήθιίκλϊΐΜΝΞΟΌΠΡΣΤΥΎΦΧΨΩΏ·μνξοόπρστυύφχψωώςϋΰ ÀÂÆÇÉÈÊËÎÏÔŒÙÛÜŸàâæçéèêëîïôœùûüÿ«“”—»–’…· @¼½¾€ ÄÖÅŠŽäöåšž ÁÀÂĀÄÃÅÆÉÈÊĒËÍÌÎĪÏÓÒÔŌÖÕØŒÚÙÛŪÜŴÝŸŶáàâāäãåæéèêēëíìîīïóòôōöõøœúùûūüŵýÿŷÞÇÐÑẞþçðñß«»“„”‘‚—’–¿¡·…@¼½¾€");
					symbolBuilder.BuildRanges(out ImVector symbolRanges);
					ImGui.GetIO().Fonts.AddFontFromFileTTF(Resource.GetResourceFilePath("Fonts/NotoSans-Regular.ttf"), Settings.FontSize, fontConfig, symbolRanges.Data);

					ImGui.GetIO().Fonts.AddFontFromFileTTF(Resource.GetResourceFilePath("Fonts/NotoSans-Regular.ttf"), Settings.FontSize, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesCyrillic());
					ImGui.GetIO().Fonts.AddFontFromFileTTF(Resource.GetResourceFilePath("Fonts/NotoSansKR-Regular.otf"), Settings.FontSize, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesKorean());
					ImGui.GetIO().Fonts.AddFontFromFileTTF(Resource.GetResourceFilePath("Fonts/NotoSansJP-Regular.otf"), Settings.FontSize, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesJapanese());
					ImGui.GetIO().Fonts.AddFontFromFileTTF(Resource.GetResourceFilePath("Fonts/NotoSansTC-Regular.otf"), Settings.FontSize, fontConfig, ImGui.GetIO().Fonts.GetGlyphRangesChineseSimplifiedCommon());

					ImFontGlyphRangesBuilderPtr iconsBuilder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
					iconsBuilder.AddText($"{MaterialDesignIcons.Settings}{MaterialDesignIcons.Search}{MaterialDesignIcons.Add_to_photos}{MaterialDesignIcons.Playlist_add}{MaterialDesignIcons.Arrow_downward}{MaterialDesignIcons.Arrow_upward}");
					iconsBuilder.BuildRanges(out ImVector iconRanges);
					fontConfig->PixelSnapH = 1;
					fontConfig->GlyphMinAdvanceX = Settings.FontSize;
					fontConfig->GlyphOffset = new Vector2(0, (Settings.FontSize - 13f) / 3f); // ??? it works
					ImGui.GetIO().Fonts.AddFontFromFileTTF(Resource.GetResourceFilePath("Fonts/MaterialIconsSharp-Regular.otf"), Settings.FontSize, fontConfig, iconRanges.Data);

					ImGui.GetIO().Fonts.Build();

					ImGuiNative.ImFontConfig_destroy(fontConfig);
				}
			);

			MainWindow.MakeCurrent();

			Task.Run(() => {
				MainWindow.SetWindowIcon(Resource.GetResourceFilePath("AppIcon.png"));
			});

			ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable | ImGuiConfigFlags.DpiEnableScaleViewports | ImGuiConfigFlags.DpiEnableScaleFonts;
			ImGui.GetIO().ConfigDockingWithShift = false;
			ImGui.GetIO().ConfigWindowsResizeFromEdges = true;
			ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = false;
			ImGui.GetIO().ConfigDockingAlwaysTabBar = true;
			ImGui.GetIO().ConfigViewportsNoTaskBarIcon = true;
			ImGui.GetIO().FontAllowUserScaling = true;
			ImGui.GetIO().WantSaveIniSettings = false;
			ImGui.GetIO().NativePtr->IniFilename = null;

			ImGui.GetStyle().FrameRounding = 0f;
			ImGui.GetStyle().WindowRounding = 0f;
			ImGui.GetStyle().TabRounding = 0f;
			ImGui.GetStyle().ScrollbarRounding = 0f;
			ImGui.GetStyle().FrameBorderSize = 0f;
			ImGui.GetStyle().FramePadding = new Vector2(4f, 2f);
			ImGui.GetStyle().WindowPadding = new Vector2(4f, 2f);
			ImGui.GetStyle().ItemSpacing = new Vector2(4f, 2f);
			ImGui.GetStyle().CellPadding = new Vector2(4f, 2f);
			ImGui.GetStyle().ItemInnerSpacing = new Vector2(4f, 2f);
			ImGui.GetStyle().IndentSpacing = 12;
			ImGui.GetStyle().ScrollbarSize = 12;
			ImGui.GetStyle().GrabMinSize = 12;
			ImGui.GetStyle().TabBorderSize = 0;
			ImGui.GetStyle().DisplaySafeAreaPadding = new Vector2(0f, 0f);
			ImGui.GetStyle().WindowMenuButtonPosition = ImGuiDir.Right;
			ImGui.GetStyle().LogSliderDeadzone = 0f;
			ImGui.GetStyle().TabMinWidthForCloseButton = 0f;
			ImGui.GetStyle().WindowMenuButtonPosition = ImGuiDir.None;
			ImGui.GetStyle().SelectableTextAlign = new Vector2(0.0f, 0.5f);

			ImGui.GetStyle().Colors[(int)ImGuiCol.ResizeGrip].W = 0f;

			ImGui.LoadIniSettingsFromDisk(Resource.GetResourceFilePath("ImGuiSettings.ini"));

			AABB aabb = new AABB {
				left = Settings.WindowPosition.X,
				top = Settings.WindowPosition.Y,
				width = Settings.WindowSize.X,
				height = Settings.WindowSize.Y
			};
			if (aabb.width != 0 && aabb.height != 0) {
				if (GlfwWindowing.IsViewGlfw(MainWindow)) {
					IMonitor m = MainWindow.GetMonitorUnderRect(aabb);

					(float xScale, float yScale) = MainWindow.GetMonitorContentScalings(m);
					ImGui.GetIO().FontGlobalScale = Settings.GuiScale * yScale;
				} else {
					ImGui.GetIO().FontGlobalScale = Settings.GuiScale;
				}

				MainWindow.Position = Settings.WindowPosition;
				MainWindow.Size = Settings.WindowSize;
			} else {
				if (GlfwWindowing.IsViewGlfw(MainWindow)) {
					(float xScale, float yScale) = MainWindow.GetMonitorContentScalings(MainWindow.GetPrimaryMonitor());
					ImGui.GetIO().FontGlobalScale = Settings.GuiScale * yScale;
				} else {
					ImGui.GetIO().FontGlobalScale = Settings.GuiScale;
				}

				MainWindow.Center(Silk.NET.Windowing.Monitor.GetMainMonitor(MainWindow));
			}

			mainPlaylistLoader.Wait();
			PlaylistWindow.ShouldReCache();
			SearchWindow.ShouldReCache();
		};
		MainWindow.Render += deltaTime => {
			ImGuiController.Update((float)deltaTime);
			ImGui.DockSpaceOverViewport(ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

			GlContext.Clear((uint)ClearBufferMask.ColorBufferBit);

			if (GlfwWindowing.IsViewGlfw(MainWindow)) {
				if (MainWindow.WindowState != WindowState.Minimized && MainWindow.HasContentScaleChanged(out (float xScale, float yScale) oldScale, out (float xScale, float yScale) newScale)) {
					MainWindow.ContentScaleWindow(newScale.yScale / oldScale.yScale);
					ImGui.GetIO().FontGlobalScale = Settings.GuiScale * newScale.yScale;
				}
			}

			//ImGui.ShowDemoWindow();
			//ImGui.ShowMetricsWindow();
			//ImGui.ShowStyleEditor();
			//ImGui.ShowUserGuide();

			MainPlaylistFolder.Update();

			SettingsWindow.Update();
			PlaylistWindow.Update();
			AddWindow.Update();
			SearchWindow.Update();

			ImGuiController.Render();

			// Reduce flickering on startup
			if (!renderOnce) {
				MainWindow.IsVisible = true;
				renderOnce = true;
			}
		};
		MainWindow.Closing += () => {
		};

		MainWindow.Run();
		MainPlaylistFolder.SaveAll();
		ImGui.SaveIniSettingsToDisk(Resource.GetResourceFilePath("ImGuiSettings.ini"));
		InputContext.Dispose();
		GlContext.Dispose();
		MainWindow.Dispose();
		MainPlaylistFolder.AwaitSaveAll();
	}
}
