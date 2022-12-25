using System.Numerics;
using ImGuiNET;
using Silk.NET.Windowing.Glfw;

namespace VidyaJunkie;

public class SettingsWindow {
	private float guiScale = Settings.GuiScale;
	private float fuzzySearchSensitivity = Settings.FuzzySearchSensitivity;

	public void Update() {
		ImGui.Begin($"{MaterialDesignIcons.Settings} Settings", ImGuiWindowFlags.NoCollapse);

		ImGui.SetNextItemWidth(-1);
		if (ImGui.SliderFloat("##Gui Scale", ref this.guiScale, 0.05f, 1.25f, $"Gui Scaling: {this.guiScale.ToString("0.00")}")) {
			this.guiScale = MathF.Floor(this.guiScale / 0.05f) * 0.05f;
			Settings.GuiScale = this.guiScale;
			if (GlfwWindowing.IsViewGlfw(Program.MainWindow)) {
				(float _, float yScale) = Program.MainWindow.GetMonitorContentScalings();
				ImGui.GetIO().FontGlobalScale = Settings.GuiScale * yScale;
			} else {
				ImGui.GetIO().FontGlobalScale = Settings.GuiScale;
			}
		}

		ImGui.SetNextItemWidth(-1);
		if (ImGui.SliderFloat("##Fuzzy Search Sensitivity", ref this.fuzzySearchSensitivity, 0f, 1f, $"Fuzzy Search Sensitivity: {this.fuzzySearchSensitivity.ToString("0.00")}")) {
			this.fuzzySearchSensitivity = MathF.Floor(this.fuzzySearchSensitivity / 0.01f) * 0.01f;
			Settings.FuzzySearchSensitivity = this.fuzzySearchSensitivity;
			Program.SearchWindow.ShouldReCache();
		}

		ImGui.SetWindowFontScale(0.8f);

		ImGui.Separator();
		float footer = (ImGui.GetTextLineHeightWithSpacing() * 2) + ImGui.GetStyle().ItemSpacing.Y;
		ImGui.BeginChild("Padding", new Vector2(-1, -footer), false, ImGuiWindowFlags.AlwaysAutoResize);
		ImGui.EndChild();

		ImGui.Separator();

		ImGui.Text("github.com/TangentOnline/VidyaJunkie");
		if (ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Click to Open");
		}

		if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
			Utilities.OpenWebLink("https://github.com/TangentOnline/VidyaJunkie");
		}

		ImGui.Text("vidya4chan.com");
		if (ImGui.IsItemHovered()) {
			ImGui.SetTooltip("Click to Open");
		}

		if (ImGui.IsItemClicked(ImGuiMouseButton.Left)) {
			Utilities.OpenWebLink("https://vidya4chan.com");
		}

		ImGui.SetWindowFontScale(1f);

		ImGui.End();
	}
}
