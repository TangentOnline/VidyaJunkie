using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace VidyaJunkie;

public interface SwapListElement {
	internal int listIndex { get; set; }
}

public static class Utilities {
	private static Mutex Mutex = new Mutex(false, Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule?.FileName));

	public static float Lerp(float fromMin, float fromMax, float fraction) {
		return (fromMin * (1f - fraction)) + (fromMax * fraction);
	}

	public static float Lerp(float fromValue, float fromMin, float fromMax, float toMin, float toMax) {
		return Math.Clamp(Lerp(toMin, toMax, (fromValue - fromMin) / (fromMax - fromMin)), MathF.Min(toMin, toMax), MathF.Max(toMin, toMax));
	}

	public static Span<T> GetSpan<T>(this List<T> list) {
		return CollectionsMarshal.AsSpan(list);
	}

	public static int SwapAdd<T>(this List<T> list, T element) {
		int index = list.Count;
		list.Add(element);
		return index;
	}

	public static void SwapRemove<T>(this List<T> list, int index) {
		list[index] = list[list.Count - 1];
		list.RemoveAt(list.Count - 1);
	}

	public static void SwapRemove<T>(this List<T> list, T element) {
		int index = list.IndexOf(element);
		list[index] = list[list.Count - 1];
		list.RemoveAt(list.Count - 1);
	}

	public static void SwapAddFast<T>(this List<T> list, T element) where T : SwapListElement {
		element.listIndex = list.Count;
		list.Add(element);
	}

	public static void SwapRemoveFast<T>(this List<T> list, T element) where T : SwapListElement {
		int listIndex = element.listIndex;
		list[listIndex] = list[list.Count - 1];
		list[listIndex].listIndex = listIndex;
		list.RemoveAt(list.Count - 1);
	}

	public static void SwapInit<T>(this List<T> list) where T : SwapListElement {
		for (int i = 0; i < list.Count; i++) {
			list[i].listIndex = i;
		}
	}

	public static int Mod(int x, int m) {
		int r = x % m;
		return r < 0 ? r + m : r;
	}

	public static int GetDeterministicHashCode(this string text) {
		unchecked {
			int hash1 = (5381 << 16) + 5381;
			int hash2 = hash1;

			for (int i = 0; i < text.Length; i += 2) {
				hash1 = ((hash1 << 5) + hash1) ^ text[i];
				if (i == text.Length - 1)
					break;
				hash2 = ((hash2 << 5) + hash2) ^ text[i + 1];
			}

			return hash1 + (hash2 * 1566083941);
		}
	}

	public static void OpenWebLink(string url) {
		try {
			Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
		} catch (Exception e) {
		}
	}

	public static bool IsProcessUnique() {
		#if RELEASE
		return Mutex.WaitOne(0, false);
		#else
		return true;
		#endif
	}


	public static void LogExceptions() {
		AppDomain.CurrentDomain.UnhandledException += (s, e) => {
			Exception exception = (e.ExceptionObject as Exception)!;

			string filePath = Resource.GetResourceFilePath("Exceptions.txt");
			StringWriter stringWriter = new StringWriter();

			stringWriter.WriteLine("-----------------------------------------------------------------------------");
			stringWriter.WriteLine($"Date : {DateTime.Now}");
			stringWriter.WriteLine();
			while (exception != null) {
				stringWriter.WriteLine(exception.GetType().FullName);
				stringWriter.WriteLine($"Message : {exception.Message}");
				stringWriter.WriteLine($"StackTrace : {exception.StackTrace}");

				exception = exception.InnerException;
			}

			StreamWriter streamWriter = new StreamWriter(filePath, true);

			#if DEBUG
			streamWriter.WriteLine(stringWriter.ToString());
			#else
			string clearedString = stringWriter.ToString();
			clearedString = clearedString.Replace(Environment.UserName, "UserName");
			clearedString = clearedString.Replace(Environment.UserDomainName, "UserDomainNane");
			clearedString = clearedString.Replace(Dns.GetHostName(), "DnsHostName");

			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			foreach (IPAddress ip in host.AddressList) {
				if (ip.AddressFamily == AddressFamily.InterNetwork) {
					clearedString = clearedString.Replace(ip.ToString(), "LocalIpAddress");
				}
			}
			streamWriter.WriteLine(clearedString);
			#endif

			streamWriter.Close();
		};
	}

	public static void DeleteFileToRecycleBin(string filepath) {
		FileSystem.DeleteFile(filepath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
	}

	public static void DeleteFolderToRecycleBin(string folderpath) {
		FileSystem.DeleteDirectory(folderpath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
	}

	public static string ReplaceAll(this string text, char[] toReplace, char replacement) {
		StringBuilder builder = new StringBuilder(text);
		Dictionary<char, bool> dict = new Dictionary<char, bool>();
		foreach (char charToReplace in toReplace) {
			dict[charToReplace] = true;
		}

		for (int i = builder.Length - 1; i >= 0; i--) {
			char currentCharacter = builder[i];
			if (dict.ContainsKey(currentCharacter)) {
				builder[i] = replacement;
			}
		}

		return builder.ToString();
	}

	public static string KeepAll(this string text, char[] toKeep) {
		StringBuilder builder = new StringBuilder(text);
		Dictionary<char, bool> dict = new Dictionary<char, bool>();
		foreach (char charToKeep in toKeep) {
			dict[charToKeep] = true;
		}

		for (int i = builder.Length - 1; i >= 0; i--) {
			char currentCharacter = builder[i];
			if (!dict.ContainsKey(currentCharacter)) {
				builder.Remove(i, 1);
			}
		}

		return builder.ToString();
	}

	public static string KeepAll(this string text, char[] toKeep, char replacement) {
		StringBuilder builder = new StringBuilder(text);
		Dictionary<char, bool> dict = new Dictionary<char, bool>();
		foreach (char charToKeep in toKeep) {
			dict[charToKeep] = true;
		}

		for (int i = builder.Length - 1; i >= 0; i--) {
			char currentCharacter = builder[i];
			if (!dict.ContainsKey(currentCharacter)) {
				builder[i] = replacement;
			}
		}

		return builder.ToString();
	}
}
