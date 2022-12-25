using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ImGuiNET;
using NicoApi;
using Xabe.FFmpeg;
using YoutubeExplode;
using YoutubeExplode.Channels;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos;

namespace VidyaJunkie;

public class VideoParseData {
	public string Title { get; set; }
	public string Uploader { get; set; }
	public string Domain { get; set; }
	public bool Successfull { get; set; }
	public string Url { get; set; }
}

public class AddWindow {
	private bool autoParse;
	private int parseCount;
	public bool IsParsing => this.parseCount > 0;
	private ConcurrentQueue<VideoParseData> parsedVideos = new ConcurrentQueue<VideoParseData>();
	private bool newVideosParsed;
	private string filteredPaste = "";

	private Video manualVideo = new Video();
	private string manualVideoLength = "";
	private string manualVideoUploaded = "";
	private string manualVideoUrl = "";

	private string multiLineUrlText = "";
	private bool multiLineUrlTextFocus;

	private int modeComboSelected;
	private string[] ModeCombo = { "Auto", "Manual" };

	private static readonly int MaxParsedVideos = 200;
	private static Regex UrlLinkParser = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
	private static YoutubeClient Youtube = new YoutubeClient();
	public static readonly string[] RawVideoExtensions = { ".mp4", ".webm", ".mkv", ".flv", ".avi", ".mov", ".wmv", ".m4v", ".f4v", ".swf", ".mp2" };

	public unsafe void Update() {
		ImGui.Begin($"{MaterialDesignIcons.Add_to_photos} Add", ImGuiWindowFlags.NoCollapse);

		if (!Program.FFMPEGLoaded) {
			int dots = (int)((Program.MainWindow.Time * 2) % 4);
			string text = $"Downloading FFMPEG{new string('.', dots)}";
			float windowWidth = ImGui.GetWindowSize().X;
			float windowHeight = ImGui.GetWindowSize().Y;
			float textWidth = ImGui.CalcTextSize(text, windowWidth).X;

			ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
			ImGui.SetCursorPosY((windowHeight / 2));
			ImGui.TextWrapped(text);
			ImGui.End();
			return;
		}

		if (!Program.PlaylistWindow.AnyPlaylistSelected()) {
			string text = "Select a playlist to add videos.";
			float windowWidth = ImGui.GetWindowSize().X;
			float windowHeight = ImGui.GetWindowSize().Y;
			float textWidth = ImGui.CalcTextSize(text, windowWidth).X;

			ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
			ImGui.SetCursorPosY((windowHeight / 2));
			ImGui.TextWrapped(text);
			ImGui.End();
			return;
		}

		ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - (ImGui.CalcTextSize("Clipboard").X + (ImGui.GetFrameHeightWithSpacing() * 2)));
		if (ImGui.Combo("##AddComboBox", ref this.modeComboSelected, this.ModeCombo, this.ModeCombo.Length)) {
			this.manualVideo = new Video();
			this.manualVideoLength = "";
			this.manualVideoUploaded = "";
			this.manualVideoUrl = "";
			this.multiLineUrlText = "";
		}

		if (this.modeComboSelected == 0) {
			ImGui.SameLine();

			if (ImGui.Checkbox("Clipboard", ref this.autoParse)) {
				Program.InputContext.GetClipboard();
			}

			if (ImGui.IsItemHovered()) {
				ImGui.SetTooltip("Automatically Parse Videos When Copied to Clipboard");
			}

			if (this.autoParse) {
				if (Program.InputContext.GetClipboardIfNew(out string clipboard)) {
					Task.Run(() => {
						this.ParseText(clipboard, Program.PlaylistWindow.GetSelectedPlaylists());
					});
				}
			}
		}

		float comboFooter = 1f;
		if (this.parsedVideos.Count > 0) {
			comboFooter = ImGui.GetWindowHeight() * 0.4f;
		}

		bool isParsingThisFrame = this.IsParsing;
		if (isParsingThisFrame) {
			comboFooter += ImGui.GetTextLineHeight() + 4;
		}

		ImGui.BeginChild("ComboChild", new Vector2(-1f, -comboFooter), false);
		ImGui.Separator();

		if (this.modeComboSelected == 0) {
			void MultilineParser() {
				string localCopy = new string(this.multiLineUrlText);
				this.multiLineUrlText = "";
				this.multiLineUrlTextFocus = true;
				Task.Run(() => {
					this.ParseText(localCopy, Program.PlaylistWindow.GetSelectedPlaylists());
				});
			}

			if (this.multiLineUrlTextFocus) {
				ImGui.SetKeyboardFocusHere();
			}

			float parseFooter = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();
			if (ImGui.InputTextMultiline("##MultiLineAddInput", ref this.multiLineUrlText, 1024 * 64, new Vector2(-1f, -parseFooter), ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CtrlEnterForNewLine | ImGuiInputTextFlags.AllowTabInput | ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackEdit, data => {
				    if (ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.V) && (data->EventFlag & ImGuiInputTextFlags.CallbackEdit) != 0) {
						data->CursorPos = Math.Max(0, data->BufTextLen - Program.InputContext.GetClipboard().Length);
					}
					if (this.multiLineUrlTextFocus) {
						data->CursorPos = data->BufTextLen;
						this.multiLineUrlTextFocus = false;
					}

					return 1;
			    })) {
				MultilineParser();
			}

			if (this.multiLineUrlText == "") {
				Vector2 itemRectMin = ImGui.GetItemRectMin();
				Vector2 itemRectMax = ImGui.GetItemRectMax();
				ImGui.GetWindowDrawList().AddText(new Vector2(itemRectMin.X + 4, itemRectMin.Y), ImGui.GetColorU32(ImGuiCol.TextDisabled), "Paste Urls Here");
			}

			// we cant change multiLineUrlText when the widget is focused, so we detect for a paste, then change focus to other widget, then filter the multiLineUrlText, then set focus back
			bool filterMultiLineUrl = false;;
			if (ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.V) && ImGui.IsItemFocused()) {
				ImGui.SetKeyboardFocusHere();
				filterMultiLineUrl = true;
			}

			if (ImGui.Button("Add", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
				MultilineParser();
			}
			if (ImGui.IsItemHovered()) {
				ImGui.SetTooltip("Parse Url's From Input and Add to Selected Playlists");
			}

			if (filterMultiLineUrl) {
				Task.Run(() => {
					string localCopy = new string(this.multiLineUrlText);

					localCopy = localCopy.KeepAll("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~:/?#[]@!$&'()*+,;=".ToCharArray(), ' ');
					localCopy = localCopy.ReplaceAll(@";,[]{}()'""".ToCharArray(), ' ');

					StringBuilder sb = new StringBuilder();
					foreach (Match match in UrlLinkParser.Matches(localCopy)) {
						sb.Append(match.Value);
						sb.Append("\n");
					}

					localCopy = sb.ToString();
					this.multiLineUrlText = localCopy;
					this.multiLineUrlTextFocus = true;
				});
			}

			ImGui.SameLine();

			if (ImGui.Button("Clear", new Vector2(-1f, 0f))) {
				this.multiLineUrlText = "";
			}

			if (ImGui.IsItemHovered()) {
				ImGui.SetTooltip("Clear Input Field");
			}
		} else if (this.modeComboSelected == 1) {
			void InputTextWidget(string ID, string inputHint, ref string onEnterText, Action onEnter) {
				ImGui.SetNextItemWidth(-1);
				if (ImGui.InputTextWithHint($"##{ID}", inputHint, ref onEnterText, 1024, ImGuiInputTextFlags.EnterReturnsTrue)) {
					onEnter();
				}
			}

			void ManualParser() {
				if (TimeSpan.TryParse(this.manualVideoLength, out TimeSpan length)) {
					this.manualVideo.videoLength = length;
				}

				if (DateTime.TryParse(this.manualVideoUploaded, new NumberFormatInfo(), out DateTime date)) {
					this.manualVideo.videoDateUploaded = date;
				}

				this.manualVideo.videoUrl = this.manualVideoUrl;
				Video localCopy = this.manualVideo.Clone();
				this.manualVideo = new Video();
				this.manualVideoUploaded = "";
				this.manualVideoLength = "";
				this.manualVideoUrl = "";

				Task.Run(() => {
					this.AddVideo(localCopy, Program.PlaylistWindow.GetSelectedPlaylists());
				});
			}

			float manualFooter = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();
			ImGui.BeginChild("AddInputFields", new Vector2(-1f, -manualFooter), false);

			InputTextWidget("VideoName", "Video Title*", ref this.manualVideo.videoName, ManualParser);
			InputTextWidget("VideoUrl", "Video Url*", ref this.manualVideoUrl, ManualParser);
			InputTextWidget("UploaderName", "Uploader Name", ref this.manualVideo.uploaderName, ManualParser);
			InputTextWidget("UploaderUrl", "Uploader Url", ref this.manualVideo.uploaderUrl, ManualParser);
			InputTextWidget("VideoLength", "Video Length (HH:MM:SS)", ref this.manualVideoLength, ManualParser);
			InputTextWidget("VideoUploaded", "Video Uploaded (YYYY/MM/DD)", ref this.manualVideoUploaded, ManualParser);
			InputTextWidget("Domain", "Domain", ref this.manualVideo.uploaderDomain, ManualParser);

			ImGui.EndChild();

			if (ImGui.Button("Add", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
				ManualParser();
			}

			if (ImGui.IsItemHovered()) {
				ImGui.SetTooltip("Add Video to Selected Playlists");
			}

			ImGui.SameLine();

			if (ImGui.Button("Clear", new Vector2(-1f, 0f))) {
				this.manualVideo = new Video();
				this.manualVideoLength = "";
				this.manualVideoUploaded = "";
				this.manualVideoUrl = "";
			}

			if (ImGui.IsItemHovered()) {
				ImGui.SetTooltip("Clear all Input Fields");
			}
		}

		ImGui.EndChild();

		if (isParsingThisFrame) {
			int dots = (int)((Program.MainWindow.Time * 2) % 4);
			string plural = this.parseCount > 1 ? "Queries" : "Query";
			string text = $"Parsing {this.parseCount} {plural}{new string('.', dots)}";
			float windowWidth = ImGui.GetWindowSize().X;
			float textWidth = ImGui.CalcTextSize($"Parsing {this.parseCount} {plural}...").X;

			ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
			ImGui.TextWrapped(text);
		}

		if (this.parsedVideos.Count > 0) {
			ImGui.SetWindowFontScale(0.8f);
			ImGui.BeginChild("PlaylistAddedList", new Vector2(-1f, 0f), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize);
			if (this.newVideosParsed) {
				ImGui.SetScrollHereY();
				this.newVideosParsed = false;
			}

			foreach (VideoParseData video in this.parsedVideos.Reverse()) {
				bool trashVal = false;

				ImGui.Selectable("\n", ref trashVal, ImGuiSelectableFlags.AllowItemOverlap | ImGuiSelectableFlags.DontClosePopups);
				if (!ImGui.GetIO().KeyCtrl && ImGui.IsItemClicked(ImGuiMouseButton.Left) && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) {
					this.manualVideoUrl = video.Url;
					this.modeComboSelected = 1;
					Utilities.OpenWebLink(video.Url);
				}

				ImGui.SameLine();

				if (ImGui.IsRectVisible(ImGui.GetItemRectSize())) {
					Texture texture = Texture.GetDomainTexture(video.Domain);
					ImGui.Image(texture.GetIntPtr(), new Vector2(ImGui.GetTextLineHeight()));
					ImGui.SameLine();

					if (video.Successfull) {
						ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0f, 1f, 0f, 1f));
						ImGui.Text($"{video.Title} : {video.Uploader}");
						ImGui.PopStyleColor();
					} else {
						ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0f, 0f, 1f));
						ImGui.Text($"Failed: {video.Url}");
						ImGui.PopStyleColor();

						if (ImGui.IsItemHovered()) {
							ImGui.BeginTooltip();
							ImGui.Text("Double Click to Open in Manual Window");
							ImGui.EndTooltip();
						}
					}
				}

				ImGui.Separator();
			}

			ImGui.EndChild();
			ImGui.SetWindowFontScale(1f);
		}

		ImGui.End();
	}

	public void AddVideo(Video video, List<Playlist> playlists) {
		if (!video.IsValid()) {
			return;
		}

		foreach (Playlist playlist in playlists) {
			Video vidClone = video.Clone();
			vidClone.inPlaylist = playlist;
			vidClone.SaveVideoThumbnailToDisk();
			playlist.AddVideo(vidClone);
		}

		this.AddToParsedTable(video, true);
		Program.PlaylistWindow.ShouldReCache();
	}

	private void AddToParsedTable(Video video, bool successfull) {
		this.parsedVideos.Enqueue(new VideoParseData { Title = video.videoName, Uploader = video.uploaderName, Url = video.videoUrl, Domain = video.uploaderDomain, Successfull = successfull });
		this.newVideosParsed = true;
		while (this.parsedVideos.Count > MaxParsedVideos) {
			this.parsedVideos.TryDequeue(out VideoParseData vid);
		}
	}

	public void ParseText(string text, List<Playlist> playlists) {
		try {
			text = text.KeepAll("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~:/?#[]@!$&'()*+,;=".ToCharArray(), ' ');
			text = text.ReplaceAll(@";,[]{}()'""".ToCharArray(), ' ');
			foreach (Match matchedLink in UrlLinkParser.Matches(text)) {
				Task.Run(() => {
					Interlocked.Add(ref this.parseCount, 1);
					Uri link = new Uri(matchedLink.Value);
					if (link.Host.Contains("youtube.com", StringComparison.CurrentCultureIgnoreCase) || link.Host.Contains("youtu.be", StringComparison.CurrentCultureIgnoreCase)) {
						VideoId? videoID = VideoId.TryParse(link.OriginalString);
						if (videoID != null) {
							this.ParseYoutubeVideo(link, playlists);
						} else {
							PlaylistId? playlistID = PlaylistId.TryParse(link.OriginalString);
							if (playlistID != null) {
								this.ParseYoutubePlaylist((PlaylistId)playlistID, playlists).Wait();
							} else {
								ChannelId? channelID = ChannelId.TryParse(link.OriginalString);
								if (channelID == null) {
									try {
										channelID = Youtube.Channels.GetByHandleAsync(link.OriginalString).Result.Id;
									} catch (Exception e) { }
								}

								if (channelID == null) {
									try {
										channelID = Youtube.Channels.GetBySlugAsync(link.OriginalString).Result.Id;
									} catch (Exception e) { }
								}

								if (channelID == null) {
									try {
										channelID = Youtube.Channels.GetByUserAsync(link.OriginalString).Result.Id;
									} catch (Exception e) { }
								}

								if (channelID != null) {
									this.ParseYoutubeChannel((ChannelId)channelID, playlists).Wait();
								}
							}
						}
					} else if (link.Host.Contains("vimeo.com", StringComparison.CurrentCultureIgnoreCase)) {
						this.ParseVimeoVideo(link, playlists);
					} else if (link.Host.Contains("dailymotion.com", StringComparison.CurrentCultureIgnoreCase)) {
						this.ParseDailymotionVideo(link, playlists);
					} else if (link.Host.Contains("streamable.com", StringComparison.CurrentCultureIgnoreCase)) {
						this.ParseStreamableVideo(link, playlists);
					} else if (link.Host.Contains("nicovideo.jp", StringComparison.CurrentCultureIgnoreCase)) {
						this.ParseNicoVideo(link, playlists);
					} else if (RawVideoExtensions.Any(e => link.OriginalString.EndsWith(e, StringComparison.CurrentCultureIgnoreCase))) {
						this.ParseWebVideo(link, playlists);
					}

					Interlocked.Add(ref this.parseCount, -1);
				});
			}
		} catch (Exception e) {
			Interlocked.Add(ref this.parseCount, -1);
		}
	}

	public void ParseYoutubeVideo(Uri link, List<Playlist> playlists) {
		Video video = new Video();
		video.videoUrl = link.OriginalString;

		try {
			YoutubeExplode.Videos.Video youtubeVideo = Youtube.Videos.GetAsync(link.OriginalString).Result;
			video.videoName = youtubeVideo.Title;
			video.videoUrl = youtubeVideo.Url;
			video.uploaderName = youtubeVideo.Author.ChannelTitle;
			video.uploaderUrl = youtubeVideo.Author.ChannelUrl;
			if (youtubeVideo.Duration != null) {
				video.videoLength = youtubeVideo.Duration.Value;
			}

			video.videoDateUploaded = youtubeVideo.UploadDate.DateTime;
			video.videoDateAdded = DateTime.Now;
			video.videoThumbnailUrl = youtubeVideo.Thumbnails.OrderBy(t => t.Resolution.Area).Last().Url;
			video.uploaderDomain = "Youtube";

			this.AddVideo(video, playlists);
		} catch (Exception e) {
			this.AddToParsedTable(video, false);
		}
	}

	public async Task ParseYoutubePlaylist(PlaylistId id, List<Playlist> playlists) {
		try {
			List<Task> tasks = new List<Task>();
			await foreach (PlaylistVideo playlistVideo in Youtube.Playlists.GetVideosAsync(id)) {
				tasks.Add(Task.Run(() => {
					this.ParseYoutubeVideo(new Uri(playlistVideo.Url), playlists);
				}));
			}

			Task.WaitAll(tasks.ToArray());
		} catch (Exception e) { }
	}

	private async Task ParseYoutubeChannel(ChannelId id, List<Playlist> playlists) {
		try {
			List<Task> tasks = new List<Task>();
			await foreach (PlaylistVideo channelVideo in Youtube.Channels.GetUploadsAsync(id)) {
				tasks.Add(Task.Run(() => {
					this.ParseYoutubeVideo(new Uri(channelVideo.Url), playlists);
				}));
			}

			Task.WaitAll(tasks.ToArray());
		} catch (Exception e) { }
	}

	public void ParseVimeoVideo(Uri link, List<Playlist> playlists) {
		Video video = new Video();
		video.videoUrl = link.OriginalString;

		try {
			string videoID = link.OriginalString.Split('/')[^1];
			byte[] jsonResponse = Program.HttpClient.GetByteArrayAsync($"https://vimeo.com/api/v2/video/{videoID}.json").Result;
			JsonObject jsonObject = (JsonObject)JsonNode.Parse(jsonResponse)[0];

			video.videoName = (string)jsonObject["title"];
			video.videoUrl = (string)jsonObject["url"];
			video.uploaderName = (string)jsonObject["user_name"];
			video.uploaderUrl = (string)jsonObject["user_url"];
			video.videoLength = TimeSpan.FromSeconds((double)jsonObject["duration"]);
			video.videoDateUploaded = DateTime.Parse((string)jsonObject["upload_date"]);
			video.videoDateAdded = DateTime.Now;
			video.uploaderDomain = "Vimeo";
			video.videoThumbnailUrl = (string)jsonObject["thumbnail_large"];

			this.AddVideo(video, playlists);
		} catch (Exception e) {
			this.AddToParsedTable(video, false);
		}
	}

	public void ParseDailymotionVideo(Uri link, List<Playlist> playlists) {
		Video video = new Video();
		video.videoUrl = link.OriginalString;

		try {
			string videoID = link.OriginalString.Split('/')[^1];
			Task<HttpResponseMessage> thumbnailUrl = Program.HttpClient.GetAsync($"https://www.dailymotion.com/thumbnail/video/{videoID}");
			byte[] jsonResponse = Program.HttpClient.GetByteArrayAsync(@$"https://api.dailymotion.com/video/{videoID}").Result;
			JsonObject jsonObject = (JsonObject)JsonNode.Parse(jsonResponse);

			video.videoName = (string)jsonObject["title"];
			video.videoUrl = link.OriginalString;
			video.uploaderName = "";
			video.uploaderUrl = (string)jsonObject["user_url"];
			video.videoLength = TimeSpan.Zero;
			video.videoDateUploaded = DateTime.UnixEpoch;
			video.videoDateAdded = DateTime.Now;
			video.uploaderDomain = "Dailymotion";
			video.videoThumbnailUrl = thumbnailUrl?.Result?.RequestMessage?.RequestUri?.OriginalString;

			this.AddVideo(video, playlists);
		} catch (Exception e) {
			this.AddToParsedTable(video, false);
		}
	}

	public void ParseStreamableVideo(Uri link, List<Playlist> playlists) {
		Video video = new Video();
		video.videoUrl = link.OriginalString;

		try {
			string videoID = link.OriginalString.Split('/')[^1];
			byte[] jsonResponse = Program.HttpClient.GetByteArrayAsync($"https://api.streamable.com/videos/{videoID}").Result;
			JsonObject jsonObject = (JsonObject)JsonNode.Parse(jsonResponse);

			video.videoName = (string)jsonObject["title"];
			video.videoUrl = link.OriginalString;
			video.videoLength = TimeSpan.FromSeconds((double)jsonObject["files"]["original"]["duration"]);
			video.videoDateUploaded = DateTime.UnixEpoch;
			video.videoDateAdded = DateTime.Now;
			video.uploaderDomain = "Streamable";

			video.videoThumbnailUrl = (string)jsonObject["thumbnail_url"];
			if (!video.videoThumbnailUrl.StartsWith("https://")) {
				video.videoThumbnailUrl = $"https:{video.videoThumbnailUrl}";
			}

			this.AddVideo(video, playlists);
		} catch (Exception e) {
			this.AddToParsedTable(video, false);
		}
	}

	private void ParseNicoVideo(Uri link, List<Playlist> playlists) {
		Video video = new Video();
		video.videoUrl = link.OriginalString;

		try {
			VideoDataResult? nicoVideo = new NicoApiClient().ParseByUrlAsync(link.OriginalString).Result;

			video.videoName = nicoVideo.Title;
			video.videoUrl = link.OriginalString;
			video.uploaderName = nicoVideo.Author;
			video.uploaderUrl = $"https://www.nicovideo.jp/user/{nicoVideo.AuthorId}";
			video.videoLength = TimeSpan.FromSeconds((double)nicoVideo.LengthSeconds);
			video.videoDateAdded = DateTime.Now;
			video.videoThumbnailUrl = nicoVideo.ThumbUrl;
			video.uploaderDomain = "NicoVideo";

			if (nicoVideo.UploadDate != null) {
				video.videoDateUploaded = (DateTime)nicoVideo.UploadDate?.DateTime;
			}

			this.AddVideo(video, playlists);
		} catch (Exception e) {
			this.AddToParsedTable(video, false);
		}
	}

	public void ParseWebVideo(Uri link, List<Playlist> playlists) {
		Video video = new Video();
		video.videoUrl = link.OriginalString;

		try {
			TimeSpan vidLength = FFmpeg.GetMediaInfo(link.OriginalString).Result.Duration;

			string host = link.Host;
			if (host.Contains("discord", StringComparison.CurrentCultureIgnoreCase)) {
				host = "Discord";
			}

			video.videoName = Path.GetFileNameWithoutExtension(link.OriginalString);
			video.videoUrl = link.OriginalString;
			video.videoLength = vidLength;
			video.videoDateAdded = DateTime.Now;
			video.uploaderDomain = host;
			video.videoThumbnailUrl = link.OriginalString;

			this.AddVideo(video, playlists);
		} catch (Exception e) {
			this.AddToParsedTable(video, false);
		}
	}
}
