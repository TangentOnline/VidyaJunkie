using System.Buffers;
using System.Numerics;
using CommunityToolkit.HighPerformance;
using Silk.NET.Core;
using Silk.NET.GLFW;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.SDL;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Glfw;
using Silk.NET.Windowing.Sdl;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Monitor = Silk.NET.Windowing.Monitor;
using VideoMode = Silk.NET.Windowing.VideoMode;

namespace VidyaJunkie;

public static class SilkWindowExtensions {
	private static (float OldXScale, float OldYScale) PreviousContentScales;
	private static string PreviousClipboard = "";

	public static unsafe string GetClipboard(this IInputContext input) {
		try {
			string clipboard = new string(input.Keyboards[0].ClipboardText);
			PreviousClipboard = clipboard;
			return clipboard;
		} catch (Exception e) {
			return PreviousClipboard;
		}
	}

	public static unsafe void SetClipboard(this IInputContext input, string clipboard) {
		try {
			input.Keyboards[0].ClipboardText = new string(clipboard);
		} catch (Exception e) { }
	}

	public static void SetPreviousClipboard(this IInputContext input, string clipboard) {
		PreviousClipboard = new string(clipboard);
	}

	public static unsafe bool GetClipboardIfNew(this IInputContext input, out string clipboard) {
		clipboard = null;
		try {
			clipboard = new string(input.Keyboards[0].ClipboardText);
			if (clipboard == "") {
				return false;
			}

			if (clipboard == PreviousClipboard) {
				return false;
			}

			if (PreviousClipboard == "") {
				PreviousClipboard = clipboard;
				return false;
			}

			PreviousClipboard = clipboard;
			return true;
		} catch (Exception e) {
			return false;
		}
	}

	public static bool IsKeyPressed(this IInputContext inputContext, Key key) {
		IReadOnlyList<IKeyboard> keyboards = inputContext.Keyboards;
		if (keyboards.Count > 0) {
			foreach (IKeyboard keyboard in keyboards) {
				if (keyboard.IsKeyPressed(key)) {
					return true;
				}
			}
		}

		return false;
	}

	public static void SetWindowIcon(this IWindow window, string filepath) {
		Configuration config = new Configuration();
		config.PreferContiguousImageBuffers = true;

		if (SdlWindowing.IsViewSdl(Program.MainWindow)) {
			using (Image<Abgr32> image = Image.Load<Abgr32>(config, filepath, TextureUtilities.GetDecoder(filepath))) {

				if (!image.DangerousTryGetSinglePixelMemory(out Memory<Abgr32> memory)) {
					throw new Exception("This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");
				}

				using (MemoryHandle pinHandle = memory.Pin()) {
					RawImage windowIcon = new RawImage(image.Width, image.Height, memory.AsBytes());
					window.SetWindowIcon(ref windowIcon);
				}
			}
		} else {
			using (Image<Rgba32> image = Image.Load<Rgba32>(config, filepath, TextureUtilities.GetDecoder(filepath))) {

				if (!image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> memory)) {
					throw new Exception("This can only happen with multi-GB images or when PreferContiguousImageBuffers is not set to true.");
				}

				using (MemoryHandle pinHandle = memory.Pin()) {
					RawImage windowIcon = new RawImage(image.Width, image.Height, memory.AsBytes());
					window.SetWindowIcon(ref windowIcon);
				}
			}
		}
	}

	public static void ContentScaleWindow(this IWindow window, float scaleChangeY) {
		Vector2D<int> oldSize = window.Size;
		window.Size = (Vector2D<int>)(new Vector2D<float>(window.Size.X, window.Size.Y) * scaleChangeY);
		Vector2D<int> newSize = window.Size;
		window.Position += new Vector2D<int>((oldSize.X - newSize.X) / 2, 0);
	}

	public static unsafe (float xScale, float yScale) GetMonitorContentScalings(this IWindow window, IMonitor monitor) {
		if (GlfwWindowing.IsViewGlfw(window)) {
			GlfwWindowing.GetExistingApi(window).GetMonitorContentScale(GlfwWindowing.GetExistingApi(window).GetMonitors(out int monCount)[monitor.Index], out float xScale, out float yScale);
			return (xScale, yScale);
		}
		if (SdlWindowing.IsViewSdl(window)) {
			float ddpi = 0;
			float vdpi = 0;
			float hdpi = 0;
			SdlWindowing.GetExistingApi(window).GetDisplayDPI(monitor.Index, ref ddpi, ref hdpi, ref vdpi);
			return (hdpi / 96f, vdpi / 96f);
		}

		throw new Exception("No supported windowing API.");
	}

	public static (float xScale, float yScale) GetMonitorContentScalings(this IWindow window) {
		IMonitor monitor = window.GetMonitorUnderWindow();
		if (monitor == null) {
			monitor = window.GetPrimaryMonitor();
		}

		return window.GetMonitorContentScalings(monitor);
	}

	public static bool HasContentScaleChanged(this IWindow window, out (float xScale, float yScale) oldScale, out (float xScale, float yScale) newScale) {
		if (PreviousContentScales.OldXScale == 0) {
			PreviousContentScales = window.GetMonitorContentScalings();
		}

		(float xScale, float yScale) currScales = window.GetMonitorContentScalings();
		bool changed = PreviousContentScales != currScales;
		oldScale = PreviousContentScales;
		newScale = currScales;
		PreviousContentScales = currScales;
		return changed;
	}

	public static IMonitor GetPrimaryMonitor(this IWindow window) {
		return Monitor.GetMainMonitor(window);
	}

	public static IMonitor? GetMonitorUnderMouse(this IWindow window, IInputContext input) {
		IMonitor? monitor = null;

		double xCursorPos = input.Mice[0].Position.X;
		double yCursorPos = input.Mice[0].Position.Y;
		int xWindowPos = window.Position.X;
		int yWindowPos = window.Position.Y;
		// convert cursor position from window coordinates to screen coordinates
		xCursorPos += xWindowPos;
		yCursorPos += yWindowPos;

		foreach (IMonitor m in Monitor.GetMonitors(window)) {
			int xMonitorPos = m.Bounds.Origin.X;
			int yMonitorPos = m.Bounds.Origin.Y;

			VideoMode mVideoMode = m.VideoMode;
			if (
				xCursorPos > xMonitorPos &&
				xCursorPos < xMonitorPos + m.Bounds.Size.X &&
				yCursorPos > yMonitorPos &&
				yCursorPos < yMonitorPos + m.Bounds.Size.Y
			) {
				monitor = m;
				break;
			}
		}

		return monitor;
	}

	public static IMonitor GetMonitorUnderRect(this IWindow window, AABB aabb) {
		bool PointAABBCollision(Vector2 pPos, AABB aabb) {
			return (pPos.X >= aabb.left) && (pPos.X <= (aabb.left + aabb.width)) && (pPos.Y >= aabb.top) && (pPos.Y <= (aabb.top + aabb.height));
		}

		foreach (IMonitor m in Monitor.GetMonitors(window)) {
			int xMonitorPos = m.Bounds.Origin.X;
			int yMonitorPos = m.Bounds.Origin.Y;
			int xMonitorWidth = m.Bounds.Size.X;
			int yMonitorHeight = m.Bounds.Size.Y;

			AABB monitorAABB = new AABB {
				left = xMonitorPos,
				top = yMonitorPos,
				width = xMonitorWidth,
				height = yMonitorHeight
			};

			if (PointAABBCollision(aabb.Center(), monitorAABB)) {
				return m;
			}
		}

		List<(float distance, IMonitor monitor)> closestMonitor = new List<(float distance, IMonitor monitor)>();
		foreach (IMonitor m in Monitor.GetMonitors(window)) {
			int xMonitorPos = m.Bounds.Origin.X;
			int yMonitorPos = m.Bounds.Origin.Y;
			int xMonitorWidth = m.Bounds.Size.X;
			int yMonitorHeight = m.Bounds.Size.Y;

			AABB monitorAABB = new AABB {
				left = xMonitorPos,
				top = yMonitorPos,
				width = xMonitorWidth,
				height = yMonitorHeight
			};

			float distance = Vector2.DistanceSquared(aabb.TopLeft(), monitorAABB.Center());
			closestMonitor.Add((distance, m));
		}

		closestMonitor.Sort((a, b) => a.distance < b.distance ? -1 : 1);
		return closestMonitor[0].monitor;
	}

	public static IMonitor GetMonitorUnderWindow(this IWindow window) {
		int xWindowPos = window.Position.X;
		int yWindowPos = window.Position.Y;
		double xWindowWidth = window.Size.X;
		double yWindowHeight = window.Size.Y;

		AABB windowAABB = new AABB {
			left = xWindowPos,
			top = yWindowPos,
			width = (float)xWindowWidth,
			height = (float)yWindowHeight
		};

		return window.GetMonitorUnderRect(windowAABB);
	}
}

public struct AABB {
	public float left;
	public float top;
	public float width;
	public float height;

	public Vector2 Center() {
		return new Vector2(this.left + (this.width / 2f), this.top + (this.height / 2f));
	}

	public Vector2 TopLeft() {
		return new Vector2(this.left, this.top);
	}
}
