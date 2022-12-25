using System.Numerics;
using ImGuiNET;

namespace VidyaJunkie;

public class PlaylistWindow {
	private Action? modal;
	private List<Action> doAfterRender = new List<Action>();

	private List<Playlist> selectedPlaylists = new List<Playlist>();
	private List<Video> selectedPlaylistsVideos = new List<Video>();
	private List<Video> allPlaylistsVideos = new List<Video>();

	public PlaylistFolder? dragDropFolder;
	public Playlist? dragDropPlaylist;

	private bool reCache;
	private bool caching;

	public unsafe void Update() {
		ImGui.Begin($"{MaterialDesignIcons.Playlist_add} Playlists", ImGuiWindowFlags.NoCollapse);

		if (Program.MainPlaylistFolder.GetAllPlaylists().Count == 0 && Program.MainPlaylistFolder.GetAllPlaylistFolders().Count == 0) {
			string text = "Right click to add playlist.";
			float windowWidth = ImGui.GetWindowSize().X;
			float windowHeight = ImGui.GetWindowSize().Y;
			float textWidth = ImGui.CalcTextSize(text).X;

			ImGui.SetCursorPosX((windowWidth - textWidth) * 0.5f);
			ImGui.SetCursorPosY((windowHeight / 2));
			ImGui.Text(text);
		}

		void RenderPlaylist(PlaylistFolder parentFolder, Playlist playlist) {
			bool isSelected = this.selectedPlaylists.Contains(playlist);
			if (isSelected) {
				ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
			}

			if (ImGui.TreeNodeEx($"##{playlist.Name}", ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.SpanFullWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen)) {
				if (isSelected) {
					ImGui.PopStyleColor();
				}

				if (ImGui.IsItemClicked()) {
					if (ImGui.GetIO().KeyShift) {
						if (isSelected) {
							this.RemoveSelectedPlaylist(playlist);
						} else {
							this.AddSelectedPlaylist(playlist);
							isSelected = true;
						}
					} else {
						this.selectedPlaylists.Clear();
						this.AddSelectedPlaylist(playlist);
						isSelected = true;
					}
				}

				if (!Program.AddWindow.IsParsing) {
					if (ImGui.BeginDragDropSource()) {
						ImGui.SetDragDropPayload("AddWindowDragDropPayload", nint.Zero, 0);
						ImGui.Text(playlist.Name);
						this.dragDropPlaylist = playlist;
						ImGui.EndDragDropSource();

						this.dragDropFolder = null;
						Program.SearchWindow.dragDropVideo = false;
					}

					if (ImGui.BeginDragDropTarget()) {
						if (ImGui.AcceptDragDropPayload("AddWindowDragDropPayload").NativePtr != null) {
							this.doAfterRender.Add(() => {
								if (this.dragDropPlaylist != null) {
									this.RemoveSelectedPlaylist(this.dragDropPlaylist);
									this.dragDropPlaylist.Move(parentFolder);
									this.dragDropPlaylist = null;
								}

								if (this.dragDropFolder != null) {
									this.dragDropFolder.Move(parentFolder);
									this.dragDropFolder = null;
								}

								if (Program.SearchWindow.dragDropVideo) {
									foreach (Video dragDropVideo in Program.SearchWindow.selectedVideos) {
										if (this.AnyPlaylistSelected()) {
											foreach (Playlist selectedPlaylist in this.selectedPlaylists) {
												if (selectedPlaylist.ContainsVideo(dragDropVideo)) {
													selectedPlaylist.RemoveVideo(dragDropVideo.videoUrl).Wait();
												}
											}
										} else {
											List<Playlist> allPlaylists = new List<Playlist>();
											Program.MainPlaylistFolder.GetAllPlaylistsRecurse(allPlaylists);
											foreach (Playlist p in allPlaylists) {
												if (p.ContainsVideo(dragDropVideo)) {
													p.RemoveVideo(dragDropVideo.videoUrl).Wait();
												}
											}
										}
										playlist.AddVideo(dragDropVideo);
									}

									Program.SearchWindow.dragDropVideo = false;
									this.ShouldReCache();
								}
							});
						}

						ImGui.EndDragDropTarget();
					}

				}

				if (ImGui.IsPopupOpen($"Playlist Popup {playlist.Name}")) {
					Vector2 rectMin = ImGui.GetItemRectMin();
					Vector2 rectMax = ImGui.GetItemRectMax();
					ImGui.GetWindowDrawList().AddRect(rectMin, rectMax, ImGui.GetColorU32(ImGuiCol.Text));
				}

				if (ImGui.BeginPopupContextItem($"Playlist Popup {playlist.Name}", ImGuiPopupFlags.MouseButtonRight)) {
					if (Program.AddWindow.IsParsing) {
						ImGui.BeginDisabled(true);
					}

					if (ImGui.MenuItem("Remove Playlist")) {
						this.doAfterRender.Add(() => {
							this.RemoveSelectedPlaylist(playlist);
							parentFolder.RemovePlaylistOnDisk(playlist);
						});
					}

					if (ImGui.MenuItem("Rename Playlist")) {
						bool windowOpen = true;
						bool runOnce = true;
						string renameInput = "";
						this.modal = () => {
							ImGui.OpenPopup("Rename Playlist");
							Vector2 center = ImGui.GetMainViewport().GetCenter();
							ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

							if (ImGui.BeginPopupModal("Rename Playlist", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)) {
								if (runOnce) {
									ImGui.SetKeyboardFocusHere();
									runOnce = false;
								}

								if (ImGui.InputTextWithHint("##RenameFolderModalTextHint", playlist.Name, ref renameInput, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
									playlist.Rename(renameInput);
									this.modal = null;
								}

								if (ImGui.Button("Change", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
									playlist.Rename(renameInput);
									this.modal = null;
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

					if (Program.AddWindow.IsParsing) {
						ImGui.EndDisabled();
					}

					ImGui.EndPopup();
				}

				ImGui.SameLine();
				ImGui.Text($"{playlist.Name} [{playlist.GetVideoCount()}]");
				if (isSelected) {
					Vector2 rectMin = ImGui.GetItemRectMin();
					Vector2 rectMax = ImGui.GetItemRectMax();
					ImGui.GetWindowDrawList().AddLine(new Vector2(rectMin.X, rectMax.Y - 4), new Vector2(rectMax.X, rectMax.Y - 4), ImGui.GetColorU32(ImGuiCol.HeaderHovered, 1), 2f);
				}

				if (Program.SearchWindow.selectedVideos.Any(v => v.inPlaylist == playlist)) {
					ImGui.SameLine();
					ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
					ImGui.Text(" *");
					ImGui.PopStyleColor();
				}
			} else {
				if (isSelected) {
					ImGui.PopStyleColor();
				}
			}
		}

		void RenderRecursePlaylistFolder(PlaylistFolder playlistFolder) {
			void RenderPlaylistFolderPopup(PlaylistFolder folder, PlaylistFolder parentFolder) {
				if (ImGui.BeginPopupContextItem($"Playlist Popup {folder.Name}", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems)) {
					if (ImGui.MenuItem("Add Playlist")) {
						bool windowOpen = true;
						bool runOnce = true;
						string inputString = "";
						this.modal = () => {
							ImGui.OpenPopup("Add Playlist");
							Vector2 center = ImGui.GetMainViewport().GetCenter();
							ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

							if (ImGui.BeginPopupModal("Add Playlist", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)) {
								if (runOnce) {
									ImGui.SetKeyboardFocusHere();
									runOnce = false;
								}

								if (ImGui.InputTextWithHint("##MainAddPlaylistTextHint", "Playlist Name", ref inputString, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
									folder.CreatePlaylist(inputString);
									this.modal = null;
								}

								if (ImGui.Button("Add", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
									folder.CreatePlaylist(inputString);
									this.modal = null;
								}

								ImGui.SameLine();

								if (ImGui.Button("Cancel", new Vector2(-1f, 0f))) {
									this.modal = null;
								}

								ImGui.EndPopup();
							} else {
								this.modal = null;
							}
						};
					}

					if (ImGui.MenuItem("Add Folder")) {
						bool windowOpen = true;
						bool runOnce = true;
						string inputString = "";
						this.modal = () => {
							ImGui.OpenPopup("Add Folder");
							Vector2 center = ImGui.GetMainViewport().GetCenter();
							ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

							if (ImGui.BeginPopupModal("Add Folder", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)) {
								if (runOnce) {
									ImGui.SetKeyboardFocusHere();
									runOnce = false;
								}

								if (ImGui.InputTextWithHint("##MainAddFolderTextHint", "Folder Name", ref inputString, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
									folder.CreateFolder(inputString);
									this.modal = null;
								}

								if (ImGui.Button("Add", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
									folder.CreateFolder(inputString);
									this.modal = null;
								}

								ImGui.SameLine();

								if (ImGui.Button("Cancel", new Vector2(-1f, 0f))) {
									this.modal = null;
								}

								ImGui.EndPopup();
							} else {
								this.modal = null;
							}
						};
					}

					if (Program.AddWindow.IsParsing) {
						ImGui.BeginDisabled(true);
					}

					if (ImGui.MenuItem("Remove Folder")) {
						this.doAfterRender.Add(() => {
							List<Playlist> playlists = new List<Playlist>();
							folder.GetAllPlaylistsRecurse(playlists);
							foreach (Playlist playlist in playlists) {
								this.RemoveSelectedPlaylist(playlist);
							}

							parentFolder.RemoveFolderOnDisk(folder);
						});
					}

					if (ImGui.MenuItem("Rename Folder")) {
						bool windowOpen = true;
						bool runOnce = true;
						string renameInput = "";
						this.modal = () => {
							ImGui.OpenPopup("Rename Folder");
							Vector2 center = ImGui.GetMainViewport().GetCenter();
							ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

							if (ImGui.BeginPopupModal("Rename Folder", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)) {
								if (runOnce) {
									ImGui.SetKeyboardFocusHere();
									runOnce = false;
								}

								if (ImGui.InputTextWithHint("##RenameFolderModalTextHint", folder.Name, ref renameInput, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
									folder.Rename(renameInput);
									this.modal = null;
								}

								if (ImGui.Button("Change", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
									folder.Rename(renameInput);
									this.modal = null;
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

					if (Program.AddWindow.IsParsing) {
						ImGui.EndDisabled();
					}

					ImGui.Separator();

					if (ImGui.MenuItem("Select All")) {
						List<Playlist> playlists = new List<Playlist>();
						folder.GetAllPlaylistsRecurse(playlists);
						foreach (Playlist playlist in playlists) {
							if (!this.selectedPlaylists.Contains(playlist)) {
								this.AddSelectedPlaylist(playlist);
							}
						}
					}

					if (ImGui.MenuItem("Select None")) {
						List<Playlist> playlists = new List<Playlist>();
						folder.GetAllPlaylistsRecurse(playlists);
						foreach (Playlist playlist in playlists) {
							if (this.selectedPlaylists.Contains(playlist)) {
								this.RemoveSelectedPlaylist(playlist);
							}
						}
					}

					if (ImGui.MenuItem("Select Inverse")) {
						List<Playlist> playlists = new List<Playlist>();
						folder.GetAllPlaylistsRecurse(playlists);
						foreach (Playlist playlist in playlists) {
							if (this.selectedPlaylists.Contains(playlist)) {
								this.RemoveSelectedPlaylist(playlist);
							} else {
								this.AddSelectedPlaylist(playlist);
							}
						}
					}

					ImGui.EndPopup();
				}
			}

			foreach (PlaylistFolder childPlaylistFolder in playlistFolder.GetAllPlaylistFolders().OrderBy(x => x.Name)) {
				List<Playlist> playlists = new List<Playlist>();
				childPlaylistFolder.GetAllPlaylistsRecurse(playlists);
				bool anySelected = playlists.Any(playlist => this.selectedPlaylists.Contains(playlist));

				void RenderPlaylistFolderGuts() {
					if (!Program.AddWindow.IsParsing) {
						if (ImGui.BeginDragDropSource()) {
							ImGui.SetDragDropPayload("AddWindowDragDropPayload", nint.Zero, 0);
							ImGui.Text(childPlaylistFolder.Name);
							this.dragDropFolder = childPlaylistFolder;
							ImGui.EndDragDropSource();

							this.dragDropPlaylist = null;
							Program.SearchWindow.dragDropVideo = false;
						}

						if (ImGui.BeginDragDropTarget()) {
							if (ImGui.AcceptDragDropPayload("AddWindowDragDropPayload").NativePtr != null) {
								this.doAfterRender.Add(() => {
									if (this.dragDropPlaylist != null) {
										this.RemoveSelectedPlaylist(this.dragDropPlaylist);
										this.dragDropPlaylist.Move(childPlaylistFolder);
										this.dragDropPlaylist = null;
									}

									if (this.dragDropFolder != null) {
										this.dragDropFolder.Move(childPlaylistFolder);
										this.dragDropFolder = null;
									}
								});
							}

							ImGui.EndDragDropTarget();
						}
					}

					if (ImGui.IsPopupOpen($"Playlist Popup {childPlaylistFolder.Name}")) {
						Vector2 rectMin = ImGui.GetItemRectMin();
						Vector2 rectMax = ImGui.GetItemRectMax();
						ImGui.GetWindowDrawList().AddRect(rectMin, rectMax, ImGui.GetColorU32(ImGuiCol.Text));
					}

					if (ImGui.IsItemClicked() && ImGui.GetIO().KeyShift) {
						foreach (Playlist playlist in playlists) {
							if (anySelected) {
								if (this.selectedPlaylists.Contains(playlist)) {
									this.RemoveSelectedPlaylist(playlist);
								}
							} else {
								if (!this.selectedPlaylists.Contains(playlist)) {
									this.AddSelectedPlaylist(playlist);
								}
							}
						}
					}

					ImGui.SameLine();
					ImGui.Text($"{childPlaylistFolder.Name} [{childPlaylistFolder.GetVideoCountRecurse()}]");
					if (anySelected) {
						Vector2 rectMin = ImGui.GetItemRectMin();
						Vector2 rectMax = ImGui.GetItemRectMax();
						ImGui.GetWindowDrawList().AddLine(new Vector2(rectMin.X, rectMax.Y - 4), new Vector2(rectMax.X, rectMax.Y - 4), ImGui.GetColorU32(ImGuiCol.HeaderHovered, 1), 2f);
					}
				}

				if (anySelected) {
					ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.HeaderHovered));
				}

				if (ImGui.TreeNodeEx($"##{childPlaylistFolder.Name}", ImGuiTreeNodeFlags.SpanFullWidth)) {
					if (anySelected) {
						ImGui.PopStyleColor();
					}

					RenderPlaylistFolderPopup(childPlaylistFolder, playlistFolder);
					RenderPlaylistFolderGuts();
					RenderRecursePlaylistFolder(childPlaylistFolder);
					foreach (Playlist childplaylist in childPlaylistFolder.GetAllPlaylists().OrderBy(x => x.Name)) {
						RenderPlaylist(childPlaylistFolder, childplaylist);
					}

					ImGui.TreePop();
				} else {
					if (anySelected) {
						ImGui.PopStyleColor();
					}

					RenderPlaylistFolderPopup(childPlaylistFolder, playlistFolder);
					RenderPlaylistFolderGuts();
				}
			}
		}

		RenderRecursePlaylistFolder(Program.MainPlaylistFolder);
		foreach (Playlist childplaylist in Program.MainPlaylistFolder.GetAllPlaylists().OrderBy(x => x.Name)) {
			RenderPlaylist(Program.MainPlaylistFolder, childplaylist);
		}

		ImGui.InvisibleButton("InvisibleDragDropTarget", new Vector2(-1, -1));
		if (!Program.AddWindow.IsParsing) {
			if (ImGui.BeginDragDropTarget()) {
				if (ImGui.AcceptDragDropPayload("AddWindowDragDropPayload").NativePtr != null) {
					this.doAfterRender.Add(() => {
						if (this.dragDropPlaylist != null) {
							this.RemoveSelectedPlaylist(this.dragDropPlaylist);
							this.dragDropPlaylist.Move(Program.MainPlaylistFolder);
							this.dragDropPlaylist = null;
						}

						if (this.dragDropFolder != null) {
							this.dragDropFolder.Move(Program.MainPlaylistFolder);
							this.dragDropFolder = null;
						}
					});
				}

				ImGui.EndDragDropTarget();
			}
		}

		if (ImGui.BeginPopupContextWindow("", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverExistingPopup)) {
			if (ImGui.MenuItem("Add Playlist")) {
				bool windowOpen = true;
				bool runOnce = true;
				string inputString = "";
				this.modal = () => {
					ImGui.OpenPopup("Add Playlist");
					Vector2 center = ImGui.GetMainViewport().GetCenter();
					ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

					if (ImGui.BeginPopupModal("Add Playlist", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)) {
						if (runOnce) {
							ImGui.SetKeyboardFocusHere();
							runOnce = false;
						}

						if (ImGui.InputTextWithHint("##MainAddPlaylistTextHint", "Playlist Name", ref inputString, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
							Program.MainPlaylistFolder.CreatePlaylist(inputString);
							this.modal = null;
						}

						if (ImGui.Button("Add", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
							Program.MainPlaylistFolder.CreatePlaylist(inputString);
							this.modal = null;
						}

						ImGui.SameLine();

						if (ImGui.Button("Cancel", new Vector2(-1f, 0f))) {
							this.modal = null;
						}

						ImGui.EndPopup();
					} else {
						this.modal = null;
					}
				};
			}

			if (ImGui.MenuItem("Add Folder")) {
				bool windowOpen = true;
				bool runOnce = true;
				string inputString = "";
				this.modal = () => {
					ImGui.OpenPopup("Add Folder");
					Vector2 center = ImGui.GetMainViewport().GetCenter();
					ImGui.SetNextWindowPos(center, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

					if (ImGui.BeginPopupModal("Add Folder", ref windowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)) {
						if (runOnce) {
							ImGui.SetKeyboardFocusHere();
							runOnce = false;
						}

						if (ImGui.InputTextWithHint("##MainAddFolderTextHint", "Folder Name", ref inputString, 256, ImGuiInputTextFlags.EnterReturnsTrue)) {
							Program.MainPlaylistFolder.CreateFolder(inputString);
							this.modal = null;
						}

						if (ImGui.Button("Add", new Vector2(ImGui.GetWindowWidth() * 0.5f, 0f))) {
							Program.MainPlaylistFolder.CreateFolder(inputString);
							this.modal = null;
						}

						ImGui.SameLine();

						if (ImGui.Button("Cancel", new Vector2(-1f, 0f))) {
							this.modal = null;
						}

						ImGui.EndPopup();
					} else {
						this.modal = null;
					}
				};
			}

			ImGui.Separator();

			if (ImGui.MenuItem("Select All")) {
				List<Playlist> playlists = new List<Playlist>();
				Program.MainPlaylistFolder.GetAllPlaylistsRecurse(playlists);
				foreach (Playlist playlist in playlists) {
					if (!this.selectedPlaylists.Contains(playlist)) {
						this.AddSelectedPlaylist(playlist);
					}
				}
			}

			if (ImGui.MenuItem("Select None")) {
				List<Playlist> playlists = new List<Playlist>();
				Program.MainPlaylistFolder.GetAllPlaylistsRecurse(playlists);
				foreach (Playlist playlist in playlists) {
					if (this.selectedPlaylists.Contains(playlist)) {
						this.RemoveSelectedPlaylist(playlist);
					}
				}
			}

			if (ImGui.MenuItem("Select Inverse")) {
				List<Playlist> playlists = new List<Playlist>();
				Program.MainPlaylistFolder.GetAllPlaylistsRecurse(playlists);
				foreach (Playlist playlist in playlists) {
					if (this.selectedPlaylists.Contains(playlist)) {
						this.RemoveSelectedPlaylist(playlist);
					} else {
						this.AddSelectedPlaylist(playlist);
					}
				}
			}

			ImGui.EndPopup();
		}

		this.modal?.Invoke();
		ImGui.End();

		foreach (Action action in this.doAfterRender) {
			action();
		}

		this.doAfterRender.Clear();

		if (this.reCache) {
			this.ReCache();
		}
	}

	public bool AnyPlaylistSelected() {
		return this.selectedPlaylists.Count > 0;
	}

	public List<Playlist> GetSelectedPlaylists() {
		return this.selectedPlaylists;
	}

	public List<Video> GetAllSelectedPlaylistsVideos() {
		return this.selectedPlaylistsVideos;
	}

	public List<Video> GetAllPlaylistVideos() {
		return this.allPlaylistsVideos;
	}

	public void AddSelectedPlaylist(Playlist playlist) {
		if (!this.selectedPlaylists.Contains(playlist)) {
			this.selectedPlaylists.Add(playlist);
			this.ShouldReCache();
		}
	}

	public void RemoveSelectedPlaylist(Playlist playlist) {
		this.selectedPlaylists.Remove(playlist);
		this.ShouldReCache();
	}

	private void ReCache() {
		if (!this.caching) {
			this.caching = true;
			this.reCache = false;
			Task.Run(() => {
				Task cacheAllVideosTask = Task.Run(() => {
					List<Video> videos = new List<Video>(Program.MainPlaylistFolder.GetVideoCountRecurse());
					Program.MainPlaylistFolder.GetAllVideosRecurse(ref videos);
					this.allPlaylistsVideos = videos;
				});

				List<Video> videos = new List<Video>(this.selectedPlaylists.Sum(p => p.GetVideoCount()));
				foreach (Playlist playlist in this.selectedPlaylists.Reverse<Playlist>()) {
					videos.AddRange(playlist.GetAllVideos());
				}

				videos = videos.Distinct().ToList();
				this.selectedPlaylistsVideos = videos;
				cacheAllVideosTask.Wait();
				Program.SearchWindow.ShouldReCache();
				this.caching = false;
			});
		}
	}

	public void ShouldReCache() {
		this.reCache = true;
	}
}
