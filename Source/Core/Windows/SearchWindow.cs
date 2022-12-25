using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using F23.StringSimilarity;
using ImGuiNET;
using Silk.NET.Input;

namespace VidyaJunkie;

public enum VideoOrderBy {
	TITLE_ASC,
	TITLE_DESC,
	UPLOADER_ASC,
	UPLOADER_DESC,
	LENGTH_ASC,
	LENGTH_DESC,
	DATE_ASC,
	DATE_DESC
}

public class SearchWindow {
	private Action? modal;

	private string searchVideoName = "";
	private string searchUploaderName = "";
	private string searchVideoLength = "";
	private string searchVideoUploaded = "";

	private bool reCache;

	private List<Video> filteredVideos = new List<Video>();
	public List<Video> selectedVideos = new List<Video>();

	private List<string> uploaderNames = new List<string>();
	private string selectedAutoComplete = "";
	private bool autoCompleteExecute;

	private bool scrollToTop;

	private bool caching;
	private string uploaderNamesLongestName = "";

	private VideoOrderBy orderBy = VideoOrderBy.TITLE_ASC;

	public bool dragDropVideo;

	public unsafe void Update() {
		ImGui.Begin($"{MaterialDesignIcons.Search} Search", ImGuiWindowFlags.NoCollapse);

		void InputTextWidget(string ID, string inputHint, ref string refText) {
			ImGui.PushID(ID);
			if (ImGui.InputTextWithHint("", inputHint, ref refText, 256, ImGuiInputTextFlags.CallbackEdit, data => {
				    this.ShouldReCache();
				    return 0;
			    })) { }

			ImGui.PopID();
		}

		if (Program.InputContext.IsKeyPressed(Key.F1)) {
			ImGui.SetKeyboardFocusHere();
		}

		ImGui.SetNextItemWidth(ImGui.GetWindowWidth() * 0.5f);
		InputTextWidget("VideoNameSearch", "Title [F1]", ref this.searchVideoName);

		if (Program.InputContext.IsKeyPressed(Key.F3)) {
			ImGui.SetKeyboardFocusHere();
		}

		ImGui.SameLine();
		ImGui.SetNextItemWidth(-1f);
		InputTextWidget("VideoLengthSearch", "Length (>/<) (HH:MM:SS) [F3]", ref this.searchVideoLength);

		if (this.autoCompleteExecute) {
			ImGui.SetKeyboardFocusHere();
			this.autoCompleteExecute = false;
		}

		if (Program.InputContext.IsKeyPressed(Key.F2)) {
			ImGui.SetKeyboardFocusHere();
		}

		ImGui.SetNextItemWidth(ImGui.GetWindowWidth() * 0.5f);
		ImGui.InputTextWithHint("##VideoUploaderSearch", "Uploader [F2]", ref this.searchUploaderName, 256, ImGuiInputTextFlags.CallbackAlways | ImGuiInputTextFlags.CallbackEdit, data => {
			string GetText() {
				if (data->BufTextLen > 0) {
					return Encoding.UTF8.GetString(data->Buf, data->BufTextLen);
				}

				return "";
			}

			void SetText(string text) {
				byte[] bytes = Encoding.UTF8.GetBytes(text);
				Marshal.Copy(bytes, 0, new nint(data->Buf), bytes.Length);
				data->BufTextLen = bytes.Length;
				data->BufSize = bytes.Length;
				data->BufDirty = 1;
				data->CursorPos = bytes.Length;
			}

			if (data->EventFlag == ImGuiInputTextFlags.CallbackEdit) {
				this.searchUploaderName = GetText();
			}

			if (this.searchUploaderName == "") {
				this.selectedAutoComplete = "";
			}

			if (data->EventFlag == ImGuiInputTextFlags.CallbackEdit && this.searchUploaderName != "") {
				List<Video> videos;
				if (Program.PlaylistWindow.AnyPlaylistSelected()) {
					videos = Program.PlaylistWindow.GetAllSelectedPlaylistsVideos();
				} else {
					videos = Program.PlaylistWindow.GetAllPlaylistVideos();
				}

				this.uploaderNames.Clear();
				this.uploaderNamesLongestName = "";
				foreach (Video video in videos) {
					if (video.uploaderName.Contains(this.searchUploaderName, StringComparison.CurrentCultureIgnoreCase) && !this.uploaderNames.Contains(video.uploaderName)) {
						this.uploaderNames.Add(video.uploaderName);
						if (video.uploaderName.Length > this.uploaderNamesLongestName.Length) {
							this.uploaderNamesLongestName = video.uploaderName;
						}
					}
				}

				this.uploaderNames.Sort((a, b) => a.IndexOf(this.searchUploaderName, StringComparison.CurrentCultureIgnoreCase) >= b.IndexOf(this.searchUploaderName, StringComparison.CurrentCultureIgnoreCase) ? 1 : -1);
			}

			if (this.uploaderNames.Count > 0 && this.searchUploaderName != "" && this.selectedAutoComplete != this.searchUploaderName) {
				ImGui.PushAllowKeyboardFocus(false);

				bool pressedArrowKey = false;

				if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) {
					pressedArrowKey = true;
					if (this.selectedAutoComplete == "") {
						this.selectedAutoComplete = this.uploaderNames[0];
					} else {
						int index = this.uploaderNames.IndexOf(this.selectedAutoComplete);
						if (index != -1) {
							this.selectedAutoComplete = this.uploaderNames[Utilities.Mod(index + 1, this.uploaderNames.Count)];
						} else {
							this.selectedAutoComplete = this.uploaderNames[0];
						}
					}
				}

				if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) {
					pressedArrowKey = true;
					if (this.selectedAutoComplete == "") {
						this.selectedAutoComplete = this.uploaderNames[0];
					} else {
						int index = this.uploaderNames.IndexOf(this.selectedAutoComplete);
						if (index != -1) {
							this.selectedAutoComplete = this.uploaderNames[Utilities.Mod(index - 1, this.uploaderNames.Count)];
						} else {
							this.selectedAutoComplete = this.uploaderNames[0];
						}
					}
				}

				if (ImGui.IsKeyPressed(ImGuiKey.Enter)) {
					SetText(this.selectedAutoComplete);
					this.autoCompleteExecute = true;
					this.ShouldReCache();
				}

				ImGui.SetNextWindowPos(new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y));
				ImGui.BeginTooltip();
				int height = (int)(ImGui.GetTextLineHeight() * Math.Min(6, this.uploaderNames.Count));
				int width = (int)(ImGui.CalcTextSize(this.uploaderNamesLongestName).X + ImGui.GetTextLineHeight());
				ImGui.BeginChild("SearchAutoCompletePopup", new Vector2(width, height), false, ImGuiWindowFlags.AlwaysVerticalScrollbar);

				foreach (string name in this.uploaderNames) {
					bool selected = this.selectedAutoComplete == name;
					if (ImGui.Selectable($"##{name}", ref selected, ImGuiSelectableFlags.SpanAllColumns)) { }

					if (selected && pressedArrowKey) {
						ImGui.SetScrollHereY();
					}

					if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped | ImGuiHoveredFlags.AllowWhenBlockedByPopup | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem | ImGuiHoveredFlags.AllowWhenDisabled | ImGuiHoveredFlags.AnyWindow)) {
						Vector2 rectMin = ImGui.GetItemRectMin();
						Vector2 rectMax = ImGui.GetItemRectMax();
						ImGui.GetWindowDrawList().AddRectFilled(rectMin, rectMax, ImGui.GetColorU32(ImGuiCol.HeaderHovered, 1), 0f);
						if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
							this.selectedAutoComplete = name;
							SetText(this.selectedAutoComplete);
							this.autoCompleteExecute = true;
							this.ShouldReCache();
						}
					}

					ImGui.SameLine();
					ImGui.Text(name);
				}

				ImGui.EndChild();
				ImGui.EndTooltip();
				ImGui.PopAllowKeyboardFocus();
			}

			switch (data->EventFlag) {
				case ImGuiInputTextFlags.CallbackEdit:
					this.ShouldReCache();
					break;
			}

			return 1;
		});

		if (Program.InputContext.IsKeyPressed(Key.F4)) {
			ImGui.SetKeyboardFocusHere();
		}

		ImGui.SetNextItemWidth(-1f);
		ImGui.SameLine();
		InputTextWidget("VideoUploadedSearch", "Date Uploaded (>/<) (YYYY-MM-DD) [F4]", ref this.searchVideoUploaded);

		if (Program.InputContext.IsKeyPressed(Key.F5)) {
			ImGui.SetKeyboardFocusHere();
		}

		if (ImGui.Button("Clear [F5]", new Vector2(-1f, 0f)) || Program.InputContext.IsKeyPressed(Key.F5)) {
			this.searchVideoName = "";
			this.searchUploaderName = "";
			this.searchVideoLength = "";
			this.searchVideoUploaded = "";
			this.ShouldReCache();
		}

		ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));

		string infix = this.orderBy == VideoOrderBy.TITLE_DESC ? MaterialDesignIcons.Arrow_downward : this.orderBy == VideoOrderBy.TITLE_ASC ? MaterialDesignIcons.Arrow_upward : "";
		if (ImGui.Button($"Title {infix}", new Vector2(ImGui.GetWindowWidth() * 0.25f, 0f))) {
			if (this.orderBy == VideoOrderBy.TITLE_ASC) {
				this.orderBy = VideoOrderBy.TITLE_DESC;
			} else {
				this.orderBy = VideoOrderBy.TITLE_ASC;
			}

			this.SortVideos(ref this.filteredVideos);
		}

		ImGui.SameLine();
		infix = this.orderBy == VideoOrderBy.UPLOADER_DESC ? MaterialDesignIcons.Arrow_downward : this.orderBy == VideoOrderBy.UPLOADER_ASC ? MaterialDesignIcons.Arrow_upward : "";
		if (ImGui.Button($"Uploader {infix}", new Vector2(ImGui.GetWindowWidth() * 0.25f, 0f))) {
			if (this.orderBy == VideoOrderBy.UPLOADER_ASC) {
				this.orderBy = VideoOrderBy.UPLOADER_DESC;
			} else {
				this.orderBy = VideoOrderBy.UPLOADER_ASC;
			}

			this.SortVideos(ref this.filteredVideos);
		}

		ImGui.SameLine();
		infix = this.orderBy == VideoOrderBy.LENGTH_DESC ? MaterialDesignIcons.Arrow_downward : this.orderBy == VideoOrderBy.LENGTH_ASC ? MaterialDesignIcons.Arrow_upward : "";
		if (ImGui.Button($"Length {infix}", new Vector2(ImGui.GetWindowWidth() * 0.25f, 0f))) {
			if (this.orderBy == VideoOrderBy.LENGTH_ASC) {
				this.orderBy = VideoOrderBy.LENGTH_DESC;
			} else {
				this.orderBy = VideoOrderBy.LENGTH_ASC;
			}

			this.SortVideos(ref this.filteredVideos);
		}

		ImGui.SameLine();
		infix = this.orderBy == VideoOrderBy.DATE_DESC ? MaterialDesignIcons.Arrow_downward : this.orderBy == VideoOrderBy.DATE_ASC ? MaterialDesignIcons.Arrow_upward : "";
		if (ImGui.Button($"Date Uploaded {infix}", new Vector2(-1f, 0f))) {
			if (this.orderBy == VideoOrderBy.DATE_ASC) {
				this.orderBy = VideoOrderBy.DATE_DESC;
			} else {
				this.orderBy = VideoOrderBy.DATE_ASC;
			}

			this.SortVideos(ref this.filteredVideos);
		}

		ImGui.PopStyleColor();

		ImGui.Separator();

		ImGui.BeginChild("ResultsChild");

		if (this.scrollToTop) {
			ImGui.SetScrollHereY();
			this.scrollToTop = false;
		}

		void CopySelectedVideosToClipboard() {
			if (this.selectedVideos.Count < 1) {
				return;
			}
			StringBuilder sb = new StringBuilder();
			foreach (Video selectedVideo in this.selectedVideos) {
				sb.Append(selectedVideo.videoUrl);
				sb.Append(",");
			}
			sb.Remove(sb.Length - 1, 1);
			string selectedVideosUrls = sb.ToString();
			Program.InputContext.SetClipboard(selectedVideosUrls);
			Program.InputContext.SetPreviousClipboard(selectedVideosUrls);
		}

		if (ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.C) && ImGui.IsWindowFocused()) {
			CopySelectedVideosToClipboard();
		}

		List<Video> videos = this.GetSearchedVideos();
		for (int index = 0; index < videos.Count; index++) {
			Video video = videos[index];

			bool trashVal = false;

			ImGui.PushID(video.videoUrl);
			ImGui.Selectable("\n\n", ref trashVal, ImGuiSelectableFlags.SpanAllColumns);
			ImGui.PopID();

			if (ImGui.BeginDragDropSource()) {
				ImGui.SetDragDropPayload("AddWindowDragDropPayload", nint.Zero, 0);
				foreach (Video selectedVideo in this.selectedVideos) {
					ImGui.Text(selectedVideo.videoName);
				}
				ImGui.EndDragDropSource();

				this.dragDropVideo = true;
				Program.PlaylistWindow.dragDropPlaylist = null;
				Program.PlaylistWindow.dragDropFolder = null;
			}

			if (!ImGui.GetIO().KeyCtrl && !ImGui.GetIO().KeyShift && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left) && ImGui.IsItemHovered()) {
				if (AddWindow.RawVideoExtensions.Any(e => video.videoUrl.EndsWith(e))) {
					string videoHTML = @$"<center>
	<video controls>
		<source src=""{video.videoUrl}"" type=""video/{Path.GetExtension(video.videoUrl).TrimStart('.')}"" />
	</video>
</center>";

					File.WriteAllText(Resource.GetResourceFilePath("VideoPlayer.html"), videoHTML);
					Utilities.OpenWebLink(Resource.GetResourceFilePath("VideoPlayer.html"));
				} else {
					Utilities.OpenWebLink(video.videoUrl);
				}
			}

			if (!ImGui.GetIO().KeyShift && ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
				this.selectedVideos.Clear();
				this.selectedVideos.Add(video);
			}

			if (ImGui.GetIO().KeyShift && ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left)) {
				if (this.selectedVideos.Contains(video)) {
					this.selectedVideos.Remove(video);
				} else {
					this.selectedVideos.Add(video);
				}
			}

			if (ImGui.IsPopupOpen(video.videoUrl)) {
				ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.Text));
			}

			if (ImGui.BeginPopupContextItem(video.videoUrl, ImGuiPopupFlags.NoOpenOverItems | ImGuiPopupFlags.MouseButtonRight)) {
				if (ImGui.MenuItem("Open Video Url [Double Click]")) {
					Utilities.OpenWebLink(video.videoUrl);
				}

				if (ImGui.MenuItem("Copy Video Url")) {
					Program.InputContext.SetClipboard(video.videoUrl);
					Program.InputContext.SetPreviousClipboard(video.videoUrl);
				}

				if (ImGui.MenuItem($"Copy Selected Videos Urls [{this.selectedVideos.Count}] [Ctrl + C]")) {
					CopySelectedVideosToClipboard();
				}

				if (ImGui.MenuItem("Open Uploader Url")) {
					Utilities.OpenWebLink(video.uploaderUrl);
				}

				if (ImGui.MenuItem("Copy Uploader Url")) {
					Program.InputContext.SetClipboard(video.uploaderUrl);
					Program.InputContext.SetPreviousClipboard(video.uploaderUrl);
				}

				if (ImGui.MenuItem("Create Streamable Clip")) {
					Utilities.OpenWebLink($"https://streamable.com/clipper?url={video.videoUrl}");
				}

				ImGui.Separator();

				if (ImGui.MenuItem("Remove Video")) {
					if (Program.PlaylistWindow.AnyPlaylistSelected()) {
						foreach (Playlist selectedPlaylist in Program.PlaylistWindow.GetSelectedPlaylists()) {
							selectedPlaylist.RemoveVideo(video.videoUrl);
						}
					} else {
						List<Playlist> playlists = new List<Playlist>();
						Program.MainPlaylistFolder.GetAllPlaylistsRecurse(playlists);
						foreach (Playlist playlist in playlists) {
							playlist.RemoveVideo(video.videoUrl);
						}
					}

					this.selectedVideos.Remove(video);
					Program.PlaylistWindow.ShouldReCache();
				}

				if (ImGui.MenuItem($"Remove Selected Videos [{this.selectedVideos.Count}]")) {
					if (Program.PlaylistWindow.AnyPlaylistSelected()) {
						foreach (Playlist selectedPlaylist in Program.PlaylistWindow.GetSelectedPlaylists()) {
							foreach (Video selectedVideo in this.selectedVideos) {
								selectedPlaylist.RemoveVideo(selectedVideo.videoUrl);
							}
						}
					} else {
						List<Playlist> playlists = new List<Playlist>();
						Program.MainPlaylistFolder.GetAllPlaylistsRecurse(playlists);
						foreach (Playlist playlist in playlists) {
							foreach (Video selectedVideo in this.selectedVideos) {
								playlist.RemoveVideo(selectedVideo.videoUrl);
							}
						}
					}

					this.selectedVideos.Clear();
					Program.PlaylistWindow.ShouldReCache();
				}

				if (ImGui.MenuItem("Edit Video")) {
					bool windowOpen = true;
					bool runOnce = true;
					Video newVideo = video.Clone();
					string videoUrl = newVideo.videoUrl;
					string videoLength = newVideo.videoLength.ToString(@"hh\:mm\:ss");
					string videoUploaded = newVideo.videoDateUploaded.ToString("yyyy-MM-dd");
					this.modal = () => {
						ImGui.OpenPopup("Edit Video");
						Vector2 center = ImGui.GetMainViewport().GetCenter();
						ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

						if (ImGui.BeginPopupModal("Edit Video", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)) {
							void EditVideo() {
								newVideo.videoUrl = videoUrl;
								if (TimeSpan.TryParse(videoLength, out TimeSpan length)) {
									newVideo.videoLength = length;
								}

								if (DateTime.TryParse(videoUploaded, out DateTime uploaded)) {
									newVideo.videoDateUploaded = uploaded;
								}

								if (Program.PlaylistWindow.AnyPlaylistSelected()) {
									foreach (Playlist selectedPlaylist in Program.PlaylistWindow.GetSelectedPlaylists()) {
										if (selectedPlaylist.ContainsVideo(video)) {
											selectedPlaylist.RemoveVideo(video).Wait();
											selectedPlaylist.AddVideo(newVideo);
										}
									}
								} else {
									List<Playlist> playlists = new List<Playlist>();
									Program.MainPlaylistFolder.GetAllPlaylistsRecurse(playlists);
									foreach (Playlist playlist in playlists) {
										if (playlist.ContainsVideo(video)) {
											playlist.RemoveVideo(video).Wait();
											playlist.AddVideo(newVideo);
										}
									}
								}

								Program.PlaylistWindow.ShouldReCache();
								this.modal = null;
							}

							if (runOnce) {
								ImGui.SetKeyboardFocusHere();
								runOnce = false;
							}

							if (ImGui.InputTextWithHint("##EditVideoName", "Video Title", ref newVideo.videoName, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
								EditVideo();
							}

							if (ImGui.InputTextWithHint("##EditVideoUrl", "Video Url", ref videoUrl, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
								EditVideo();
							}

							if (ImGui.InputTextWithHint("##EditVideoLength", "Video Length", ref videoLength, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
								EditVideo();
							}

							if (ImGui.InputTextWithHint("##EditVideoUploaded", "Video Date Uploaded", ref videoUploaded, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
								EditVideo();
							}

							if (ImGui.InputTextWithHint("##EditUploaderName", "Uploader Name", ref newVideo.uploaderName, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
								EditVideo();
							}

							if (ImGui.InputTextWithHint("##EditUploaderUrl", "Uploader Url", ref newVideo.uploaderUrl, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
								EditVideo();
							}

							if (ImGui.InputTextWithHint("##EditUploaderDomain", "Uploader Domain", ref newVideo.uploaderDomain, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
								EditVideo();
							}

							if (ImGui.Button("Update", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
								EditVideo();
							}

							ImGui.SameLine();

							if (ImGui.Button("Cancel", new Vector2(-1f, -0f))) {
								this.modal = null;
							}

							ImGui.EndPopup();
						} else {
							this.modal = null;
						}
					};
				}

				ImGui.EndPopup();
			}

			ImGui.SameLine();

			if (ImGui.IsRectVisible(ImGui.GetItemRectSize())) {
				bool selected = this.selectedVideos.Contains(video);

				if (selected) {
					ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.HeaderHovered));
				}

				Texture thumbnailTexture = video.GetThumbnailTexture();
				ImGui.Image(thumbnailTexture.GetIntPtr(), new Vector2(ImGui.GetTextLineHeight() * 2));
				ImGui.SameLine();

				string first = video.videoName;
				float firstStart = ImGui.GetCursorPosX();
				string second = video.uploaderName;
				string third = $"{video.videoLength.ToString(@"hh\:mm\:ss")} / {video.videoDateUploaded.ToString("yyyy-MM-dd")}";
				float thirdStart = ImGui.GetWindowWidth() - ImGui.CalcTextSize(third).X - ImGui.GetStyle().ItemSpacing.X - ImGui.GetStyle().ScrollbarSize - 4;

				if (firstStart + ImGui.CalcTextSize(first).X > thirdStart - 4) {
					string firstTrimmed = first.Substring(0, first.Length - 3) + "...";
					while ((firstStart + ImGui.CalcTextSize(firstTrimmed).X) > thirdStart && firstTrimmed.Length > 6) {
						firstTrimmed = firstTrimmed.Substring(0, firstTrimmed.Length - 6) + "...";
					}

					first = firstTrimmed;
				}

				if ((firstStart + ImGui.CalcTextSize(second).X) > thirdStart - 4) {
					string secondTrimmed = second.Substring(0, second.Length - 3) + "...";
					while ((firstStart + ImGui.CalcTextSize(secondTrimmed).X) > thirdStart && secondTrimmed.Length > 6) {
						secondTrimmed = secondTrimmed.Substring(0, secondTrimmed.Length - 6) + "...";
					}

					second = secondTrimmed;
				}

				ImGui.Text($"{first}\n      {second}");
				ImGui.SameLine();
				ImGui.SetCursorPosX((ImGui.GetTextLineHeight() * 2) + ImGui.GetStyle().ItemSpacing.X + 4);
				ImGui.SetCursorPosY((ImGui.GetCursorPosY() + ImGui.GetTextLineHeight()));
				Texture domaintexture = Texture.GetDomainTexture(video.uploaderDomain);
				ImGui.Image(domaintexture.GetIntPtr(), new Vector2(ImGui.GetTextLineHeight()));
				ImGui.SameLine(thirdStart);
				ImGui.SetCursorPosY((ImGui.GetCursorPosY() + ImGui.GetTextLineHeight() / 2f));
				ImGui.Text(third);
			}

			ImGui.Separator();
		}

		ImGui.EndChild();
		ImGui.End();

		if (this.reCache) {
			this.ReCache();
		}

		this.modal?.Invoke();
	}

	private void ReCache() {
		if (!this.caching) {
			this.caching = true;
			this.reCache = false;
			Task.Run(() => {
				IEnumerable<Video> videos;
				if (Program.PlaylistWindow.AnyPlaylistSelected()) {
					videos = Program.PlaylistWindow.GetAllSelectedPlaylistsVideos().AsParallel();
				} else {
					videos = Program.PlaylistWindow.GetAllPlaylistVideos().AsParallel();
				}

				if (this.searchVideoName == "" && this.searchUploaderName == "" && this.searchVideoLength == "" && this.searchVideoUploaded == "") {
					this.filteredVideos = videos.ToList();
				} else {
					DateTime dateTime = DateTime.Now;
					bool dateTimeLargerThan = this.searchVideoUploaded.Contains(">");
					bool searchVideoUploaded = false;
					if (this.searchVideoUploaded != "") {
						try {
							searchVideoUploaded = true;
							string localVideoUploaded = this.searchVideoUploaded.KeepAll("1234567890-".ToCharArray());
							int dashCount = localVideoUploaded.Count(c => c == '-');
							string[] videoUploadedDashSplit = localVideoUploaded.Split('-').Where(s => s != "").ToArray();

							if (dashCount == 0) {
								dateTime = new DateTime(Convert.ToInt32(localVideoUploaded), 1, 1);
							} else if (dashCount == 1) {
								int year = 1;
								if (videoUploadedDashSplit.Length > 0) {
									year = Convert.ToInt32(videoUploadedDashSplit[0]);
								}

								int month = 1;
								if (videoUploadedDashSplit.Length > 1) {
									month = Convert.ToInt32(videoUploadedDashSplit[1]);
								}

								dateTime = new DateTime(year, month, 1);
							} else if (dashCount == 2) {
								int year = 1;
								if (videoUploadedDashSplit.Length > 0) {
									year = Convert.ToInt32(videoUploadedDashSplit[0]);
								}

								int month = 1;
								if (videoUploadedDashSplit.Length > 1) {
									month = Convert.ToInt32(videoUploadedDashSplit[1]);
								}

								int day = 1;
								if (videoUploadedDashSplit.Length > 2) {
									day = Convert.ToInt32(videoUploadedDashSplit[2]);
								}

								dateTime = new DateTime(year, month, day);
							}
						} catch (Exception e) {
							searchVideoUploaded = false;
						}
					}

					TimeSpan timeSpan = TimeSpan.Zero;
					bool timeSpanLargerThan = this.searchVideoLength.Contains(">");
					bool searchVideoLength = false;
					if (this.searchVideoLength != "") {
						try {
							searchVideoLength = true;
							string localVideoLength = this.searchVideoLength.KeepAll("1234567890:".ToCharArray());
							int colonCount = localVideoLength.Count(c => c == ':');
							string[] videoLengthColonSplit = localVideoLength.Split(':').Where(s => s != "").ToArray();

							if (colonCount == 0) {
								timeSpan = TimeSpan.FromSeconds(int.Parse(localVideoLength));
							} else if (colonCount == 1) {
								int minutes = 0;
								if (videoLengthColonSplit.Length > 0) {
									minutes = Convert.ToInt32(videoLengthColonSplit[0]);
								}

								int seconds = 0;
								if (videoLengthColonSplit.Length > 1) {
									seconds = Convert.ToInt32(videoLengthColonSplit[1]);
								}

								timeSpan = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
							} else if (colonCount == 2) {
								int hours = 0;
								if (videoLengthColonSplit.Length > 0) {
									hours = Convert.ToInt32(videoLengthColonSplit[0]);
								}

								int minutes = 0;
								if (videoLengthColonSplit.Length > 1) {
									minutes = Convert.ToInt32(videoLengthColonSplit[1]);
								}

								int seconds = 0;
								if (videoLengthColonSplit.Length > 2) {
									seconds = Convert.ToInt32(videoLengthColonSplit[2]);
								}

								timeSpan = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
							}
						} catch (Exception e) {
							searchVideoLength = false;
						}
					}

					bool searchUploader = this.searchUploaderName != "";

					bool searchVideoName = this.searchVideoName != "";
					int searchVideoNameLength = this.searchVideoName.Length;
					JaroWinkler fuzzyScorer = new JaroWinkler();
					string[] paddedVideoNameWords = this.searchVideoName.Split(" ").Where(s => s != "").Select(s => $" {s} ").ToArray();

					videos = videos.Where(video => {
						if (searchUploader) {
							if (!video.uploaderName.Contains(this.searchUploaderName, StringComparison.CurrentCultureIgnoreCase)) {
								return false;
							}
						}

						if (searchVideoLength) {
							if (timeSpanLargerThan) {
								if (video.videoLength <= timeSpan) {
									return false;
								}
							} else {
								if (video.videoLength >= timeSpan) {
									return false;
								}
							}
						}

						if (searchVideoUploaded) {
							if (dateTimeLargerThan) {
								if (video.videoDateUploaded <= dateTime) {
									return false;
								}
							} else {
								if (video.videoDateUploaded >= dateTime) {
									return false;
								}
							}
						}

						if (searchVideoName) {
							// Perfect Match, Strings are exactly alike
							if (string.Equals(video.videoName, this.searchVideoName, StringComparison.CurrentCultureIgnoreCase)) {
								video.fuzzySimilarityScore = int.MaxValue;
								return true;
							}

							float score = 0;

							// Partial Match, video contains the search term, but could be substring of a word, or multiple words chained
							if (video.videoName.Contains(this.searchVideoName, StringComparison.CurrentCultureIgnoreCase)) {
								score += 50;
							}

							// Word Match, Matching words not substrings
							if (paddedVideoNameWords.Length > 1) {
								string paddedVideoName = $" {video.videoName.TrimEnd('.', ',', '!', '?', '%')} ";
								foreach (string s in paddedVideoNameWords) {
									if (paddedVideoName.Contains(s, StringComparison.CurrentCultureIgnoreCase)) {
										score += 5;
									}
								}
							}

							// Fuzzy Search
							double sim = fuzzyScorer.Similarity(this.searchVideoName, video.videoName);
							if (sim > Settings.FuzzySearchSensitivity) {
								score += (float)sim;
							}

							video.fuzzySimilarityScore = score;
							if (video.fuzzySimilarityScore > 0) {
								return true;
							}

							return false;
						}

						return true;
					});
				}

				List<Video> vidsList = videos.ToList();
				this.SortVideos(ref vidsList);
				this.filteredVideos = vidsList;
				this.caching = false;
				this.scrollToTop = true;
			});
		}
	}

	public void SortVideos(ref List<Video> videos) {
		if (this.orderBy == VideoOrderBy.TITLE_ASC) {
			if (this.searchVideoName != "") {
				videos = videos.OrderByDescending(v => v.fuzzySimilarityScore).ToList();
			} else {
				videos = videos.OrderBy(v => v.videoName).ToList();
			}
		} else if (this.orderBy == VideoOrderBy.TITLE_DESC) {
			if (this.searchVideoName != "") {
				videos = videos.OrderByDescending(v => v.fuzzySimilarityScore).ToList();
			} else {
				videos = videos.OrderByDescending(v => v.videoName).ToList();
			}
		}

		if (this.orderBy == VideoOrderBy.UPLOADER_ASC) {
			videos = videos.OrderBy(v => v.uploaderName).ToList();
		} else if (this.orderBy == VideoOrderBy.UPLOADER_DESC) {
			videos = videos.OrderByDescending(v => v.uploaderName).ToList();
		}

		if (this.orderBy == VideoOrderBy.LENGTH_ASC) {
			videos = videos.OrderBy(v => v.videoLength).ToList();
		} else if (this.orderBy == VideoOrderBy.LENGTH_DESC) {
			videos = videos.OrderByDescending(v => v.videoLength).ToList();
		}

		if (this.orderBy == VideoOrderBy.DATE_ASC) {
			videos = videos.OrderBy(v => v.videoDateUploaded).ToList();
		} else if (this.orderBy == VideoOrderBy.DATE_DESC) {
			videos = videos.OrderByDescending(v => v.videoDateUploaded).ToList();
		}

		this.scrollToTop = true;
	}

	public void ShouldReCache() {
		this.reCache = true;
		this.scrollToTop = true;
	}

	public List<Video> GetSearchedVideos() {
		return this.filteredVideos;
	}
}
