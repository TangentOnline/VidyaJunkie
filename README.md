# Vidya Junkie
Vidya Junkie is a program to catalogue, organize and search web videos, it can quickly parse videos from Youtube (Videos, Playlists, Channels), Vimeo, NicoVideo, Dailymotion, Streamable and most Raw videos (url's ending with .mp4, .mov etc...)

![](Resource/VidyaJunkie.png)

# Features
Organize videos in folders and playlists, right click to edit, drag and drop to restructure <br />
Parse video data from urls, and add to selected playlists <br />
Automatically parse urls and add to playlists when copied to clipboard <br />
Add videos manually, supporting any url and domain <br />
Fast and smart search, Fuzzy search, suggestions, Filter videos by uploader, date and length <br />
Sort videos by title, uploader, length, date <br />
Double click result entry to open video in browser, select videos (shift click) and press Ctrl + C to copy video urls, Edit video data, Create Streamable clip, drag and drop videos to move to different playlist

# Download
You can download the program from the release page [here](https://github.com/TangentOnline/VidyaJunkie/releases)

# Build Locally
Download .NET 7 SDK: https://dotnet.microsoft.com/en-us/download/dotnet/7.0 <br />
Git Clone the repo to your local machine <br />
Open a terminal in the root directory of the repo, same folder as .sln file and .csproj file <br />
Run: "dotnet run -c Debug" for debugging and developing <br />
Run: "dotnet publish -c Release -p:ExtraDefineConstants=\"PUBLISH\"" for final publishing, .exe will be in Build/Release/pub, This will copy all of the data from Resource folder, which you might want to clear out before sharing. .exe file and Resource folder need to be kept together next to each other for the .exe to run <br />

# Known issues
Cannot render emojis or other unicode characters over unicode 2^16
Certain glyphs are not packed into the font atlas, so they are not rendered
