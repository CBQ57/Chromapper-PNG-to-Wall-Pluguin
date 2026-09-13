using System;
using System.Collections.Generic;
using System.IO;
using Beatmap.Base;
using Beatmap.Enums;
using SFB;
using SimpleJSON;
using UnityEngine;

namespace PngWall
{
    [Plugin("PNG Wall")]
    public class Plugin
    {
        private const int MaximumWallCount = 12000;
        private const int DefaultDensity = 48;
        private const float DefaultPixelSize = 0.25f;
        private const float DefaultCenterX = 0f;
        private const float DefaultBottomY = 0f;
        private const float DefaultWallDepth = 0.05f;
        private const float DefaultAlphaCutoff = 0.1f;
        private const float DefaultOpacity = 0.05f;
        private const bool DefaultSmoothSampling = false;
        private const bool DefaultDecorative = true;
        private const string DefaultValueColor = "#FFD54F";

        private readonly RasterizationOptions rasterOptions = new RasterizationOptions
        {
            Density = DefaultDensity,
            PixelSize = DefaultPixelSize,
            CenterX = DefaultCenterX,
            BottomY = DefaultBottomY,
            AlphaCutoff = DefaultAlphaCutoff,
            MaxWalls = MaximumWallCount
        };

        private string pngPath;
        private float startBeat;
        private float defaultStartBeat;
        private float opacity = DefaultOpacity;
        private float wallDepth = DefaultWallDepth;
        private bool smoothSampling = DefaultSmoothSampling;
        private bool decorative = DefaultDecorative;
        private TextComponent fileText;
        private TextComponent statusText;
        private Texture2D iconTexture;
        private Sprite iconSprite;

        [Init]
        private void Init()
        {
            iconTexture = CreateIconTexture();
            iconSprite = Sprite.Create(
                iconTexture,
                new Rect(0, 0, iconTexture.width, iconTexture.height),
                new Vector2(0.5f, 0.5f));

            ExtensionButtons.AddButton(iconSprite, "PNG画像をWallへ変換", ShowDialog);
            Debug.Log("PNG Wall plugin loaded.");
        }

        [Exit]
        private void Exit()
        {
            if (iconSprite != null)
            {
                UnityEngine.Object.Destroy(iconSprite);
            }

            if (iconTexture != null)
            {
                UnityEngine.Object.Destroy(iconTexture);
            }
        }

        private void ShowDialog()
        {
            var audioTime = UnityEngine.Object.FindObjectOfType<AudioTimeSyncController>();
            if (audioTime != null)
            {
                startBeat = audioTime.CurrentSongBpmTime;
            }
            defaultStartBeat = startBeat;

            OpenMainDialog();
        }

        private void OpenMainDialog()
        {
            var dialog = PersistentUI.Instance.CreateNewDialogBox().WithTitle("PNG Wall 1/3 - 基本");

            dialog.AddComponent<ButtonComponent>()
                .WithLabel("PNG画像を選択")
                .OnClick(SelectPng);

            fileText = dialog.AddComponent<TextComponent>()
                .WithInitialValue(GetSelectedFileText());

            dialog.AddComponent<SliderComponent>()
                .WithLabel("ピクセル密度")
                .WithSliderParams(4f, 128f, 1f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, DefaultDensity, "0"))
                .WithInitialValue((float)rasterOptions.Density)
                .OnChanged((float value) => rasterOptions.Density = Mathf.RoundToInt(value));

            dialog.AddComponent<SliderComponent>()
                .WithLabel("1pxサイズ")
                .WithSliderParams(0.05f, 1f, 0.05f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, DefaultPixelSize, "0.00"))
                .WithInitialValue(rasterOptions.PixelSize)
                .OnChanged((float value) => rasterOptions.PixelSize = value);

            dialog.AddComponent<ButtonComponent>()
                .WithLabel("Wallを生成")
                .OnClick(GenerateWalls);

            statusText = dialog.AddComponent<TextComponent>()
                .WithInitialValue("未生成");

            dialog.AddFooterButton(() =>
            {
                rasterOptions.Density = DefaultDensity;
                rasterOptions.PixelSize = DefaultPixelSize;
                dialog.Close();
                OpenMainDialog();
            }, "初期値");
            dialog.AddFooterButton(() =>
            {
                dialog.Close();
                OpenPlacementDialog();
            }, "配置 →");
            dialog.AddFooterButton(null, "閉じる");
            dialog.Open();
        }

        private void OpenPlacementDialog()
        {
            var dialog = PersistentUI.Instance.CreateNewDialogBox().WithTitle("PNG Wall 2/3 - 配置");

            dialog.AddComponent<SliderComponent>()
                .WithLabel("X中央位置")
                .WithSliderParams(-20f, 20f, 0.25f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, DefaultCenterX, "0.00"))
                .WithInitialValue(rasterOptions.CenterX)
                .OnChanged((float value) => rasterOptions.CenterX = value);

            dialog.AddComponent<SliderComponent>()
                .WithLabel("下端Y位置")
                .WithSliderParams(-5f, 20f, 0.25f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, DefaultBottomY, "0.00"))
                .WithInitialValue(rasterOptions.BottomY)
                .OnChanged((float value) => rasterOptions.BottomY = value);

            dialog.AddComponent<SliderComponent>()
                .WithLabel("開始Beat")
                .WithSliderParams(0f, 2048f, 0.25f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, defaultStartBeat, "0.00"))
                .WithInitialValue(startBeat)
                .OnChanged((float value) => startBeat = value);

            dialog.AddComponent<SliderComponent>()
                .WithLabel("Wallの奥行き (Beat)")
                .WithSliderParams(0.01f, 1f, 0.01f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, DefaultWallDepth, "0.00"))
                .WithInitialValue(wallDepth)
                .OnChanged((float value) => wallDepth = value);

            dialog.AddFooterButton(() =>
            {
                dialog.Close();
                OpenMainDialog();
            }, "← 基本");
            dialog.AddFooterButton(() =>
            {
                rasterOptions.CenterX = DefaultCenterX;
                rasterOptions.BottomY = DefaultBottomY;
                startBeat = defaultStartBeat;
                wallDepth = DefaultWallDepth;
                dialog.Close();
                OpenPlacementDialog();
            }, "初期値");
            dialog.AddFooterButton(() =>
            {
                dialog.Close();
                OpenOptionsDialog();
            }, "詳細 →");
            dialog.Open();
        }

        private void OpenOptionsDialog()
        {
            var dialog = PersistentUI.Instance.CreateNewDialogBox().WithTitle("PNG Wall 3/3 - 詳細");

            dialog.AddComponent<SliderComponent>()
                .WithLabel("薄いピクセルの除外しきい値")
                .WithSliderParams(0f, 1f, 0.05f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, DefaultAlphaCutoff, "0.00"))
                .WithInitialValue(rasterOptions.AlphaCutoff)
                .OnChanged((float value) => rasterOptions.AlphaCutoff = value);

            dialog.AddComponent<SliderComponent>()
                .WithLabel("壁の不透明度 (0=透明 / 1=不透明)")
                .WithSliderParams(0f, 1f, 0.05f)
                .WithCustomDisplayFormatter(value => FormatDefaultValue(value, DefaultOpacity, "0.00"))
                .WithInitialValue(opacity)
                .OnChanged((float value) => opacity = value);

            dialog.AddComponent<ToggleComponent>()
                .WithLabel("滑らかに縮小")
                .WithInitialValue(smoothSampling)
                .OnChanged((bool value) => smoothSampling = value);

            dialog.AddComponent<ToggleComponent>()
                .WithLabel("装飾用 (fake / 当たり判定なし)")
                .WithInitialValue(decorative)
                .OnChanged((bool value) => decorative = value);

            dialog.AddFooterButton(() =>
            {
                dialog.Close();
                OpenPlacementDialog();
            }, "← 配置");
            dialog.AddFooterButton(() =>
            {
                rasterOptions.AlphaCutoff = DefaultAlphaCutoff;
                opacity = DefaultOpacity;
                smoothSampling = DefaultSmoothSampling;
                decorative = DefaultDecorative;
                dialog.Close();
                OpenOptionsDialog();
            }, "初期値");
            dialog.AddFooterButton(() =>
            {
                dialog.Close();
                OpenMainDialog();
            }, "基本へ");
            dialog.Open();
        }

        private static string FormatDefaultValue(float value, float defaultValue, string format)
        {
            var text = value.ToString(format);
            return Mathf.Approximately(value, defaultValue)
                ? "<color=" + DefaultValueColor + ">" + text + "</color>"
                : text;
        }

        private void SelectPng()
        {
            var initialDirectory = pngPath != null
                ? Path.GetDirectoryName(pngPath)
                : Directory.GetCurrentDirectory();

            StandaloneFileBrowser.OpenFilePanelAsync(
                "PNG画像を選択",
                initialDirectory,
                "png",
                false,
                paths =>
                {
                    if (paths == null || paths.Length == 0 || string.IsNullOrWhiteSpace(paths[0]))
                    {
                        return;
                    }

                    pngPath = paths[0];
                    if (fileText != null)
                    {
                        fileText.Value = GetSelectedFileText();
                    }

                    SetStatus("画像を選択しました。設定後に「Wallを生成」を押してください。");
                });
        }

        private void GenerateWalls()
        {
            Texture2D texture = null;

            try
            {
                if (string.IsNullOrWhiteSpace(pngPath) || !File.Exists(pngPath))
                {
                    throw new InvalidOperationException("先にPNG画像を選択してください。");
                }

                if (BeatSaberSongContainer.Instance == null || BeatSaberSongContainer.Instance.Map == null)
                {
                    throw new InvalidOperationException("譜面エディターを開いてから実行してください。");
                }

                if (Settings.Instance.MapVersion >= 4)
                {
                    throw new InvalidOperationException("Beatmap V4はChroma/Noodleデータ非対応です。V2またはV3へ切り替えてください。");
                }

                var obstacleCollection =
                    BeatmapObjectContainerCollection.GetCollectionForType<ObstacleGridContainer>(ObjectType.Obstacle);

                if (obstacleCollection == null)
                {
                    throw new InvalidOperationException("Wallコレクションを取得できません。譜面を開き直してください。");
                }

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(pngPath), false))
                {
                    throw new InvalidOperationException("PNG画像を読み込めませんでした。");
                }

                Func<float, float, PixelColor> sampler = (u, v) => SampleTexture(texture, u, v);
                var rasterized = ImageWallRasterizer.Rasterize(
                    texture.width,
                    texture.height,
                    sampler,
                    rasterOptions);

                if (rasterized.Walls.Count == 0)
                {
                    throw new InvalidOperationException("しきい値を超えるピクセルがありません。透明度しきい値を下げてください。");
                }

                var map = BeatSaberSongContainer.Instance.Map;
                var startJsonTime = (float)map.SongBpmTimeToJsonTime(startBeat);
                var endJsonTime = (float)map.SongBpmTimeToJsonTime(startBeat + wallDepth);
                var duration = Math.Max(0.001f, endJsonTime - startJsonTime);

                var walls = CreateObstacles(rasterized.Walls, startJsonTime, duration);
                PlaceObstacles(obstacleCollection, walls);

                SetStatus(string.Format(
                    "完了: {0}x{1} / {2} Walls。Ctrl+Zで一括Undoできます。",
                    rasterized.Width,
                    rasterized.Height,
                    walls.Count));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                SetStatus("エラー: " + exception.Message);
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private PixelColor SampleTexture(Texture2D texture, float u, float v)
        {
            Color color;

            if (smoothSampling)
            {
                color = texture.GetPixelBilinear(Mathf.Clamp01(u), Mathf.Clamp01(v));
            }
            else
            {
                var x = Mathf.Clamp(Mathf.FloorToInt(u * texture.width), 0, texture.width - 1);
                var y = Mathf.Clamp(Mathf.FloorToInt(v * texture.height), 0, texture.height - 1);
                color = texture.GetPixel(x, y);
            }

            return new PixelColor(color.r, color.g, color.b, color.a);
        }

        private List<BaseObject> CreateObstacles(
            IEnumerable<WallPixel> pixels,
            float jsonTime,
            float duration)
        {
            var result = new List<BaseObject>();

            foreach (var pixel in pixels)
            {
                var wall = new BaseObstacle();
                wall.JsonTime = jsonTime;
                wall.Type = 0;
                wall.PosX = 0;
                wall.PosY = 0;
                wall.Width = 1;
                wall.Height = 1;
                wall.Duration = duration;

                var coordinates = new JSONArray();
                coordinates[0] = pixel.X;
                coordinates[1] = pixel.Y;
                wall.CustomCoordinate = coordinates;

                var size = new JSONArray();
                size[0] = pixel.Size;
                size[1] = pixel.Size;
                wall.CustomSize = size;

                wall.CustomColor = new Color(
                    Mathf.Clamp01(pixel.Color.Red),
                    Mathf.Clamp01(pixel.Color.Green),
                    Mathf.Clamp01(pixel.Color.Blue),
                    Mathf.Clamp01(pixel.Color.Alpha) * opacity);

                if (decorative)
                {
                    if (Settings.Instance.MapVersion == 2)
                    {
                        wall.CustomData["_fake"] = true;
                        wall.CustomData["_interactable"] = false;
                    }
                    else
                    {
                        wall.CustomFake = true;
                        wall.CustomData["uninteractable"] = true;
                    }
                }

                wall.WriteCustom();
                result.Add(wall);
            }

            return result;
        }

        private static void PlaceObstacles(
            ObstacleGridContainer collection,
            List<BaseObject> walls)
        {
            var placed = new List<BaseObject>();
            var removedConflicts = new List<BaseObject>();

            try
            {
                foreach (var wall in walls)
                {
                    List<BaseObject> conflicts;
                    collection.SpawnObject(wall, out conflicts, true, false, true);
                    placed.Add(wall);
                    removedConflicts.AddRange(conflicts);
                }

                collection.DoPostObjectsSpawnedWorkflow();
                collection.RefreshPool(true);
                BeatmapActionContainer.AddAction(new BeatmapObjectPlacementAction(
                    placed,
                    removedConflicts,
                    "Generate PNG wall art."));
            }
            catch
            {
                for (var index = placed.Count - 1; index >= 0; index--)
                {
                    collection.DeleteObject(
                        placed[index],
                        false,
                        false,
                        "Rollback PNG wall art.",
                        true,
                        false);
                }

                foreach (var removed in removedConflicts)
                {
                    collection.SpawnObject(removed, false, false, true);
                }

                collection.RefreshPool(true);
                throw;
            }
        }

        private string GetSelectedFileText()
        {
            return string.IsNullOrWhiteSpace(pngPath)
                ? "画像: 未選択"
                : "画像: " + Path.GetFileName(pngPath);
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.Value = message;
            }

            Debug.Log("PNG Wall: " + message);
        }

        private static Texture2D CreateIconTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;

            var background = new Color(0.08f, 0.09f, 0.14f, 1f);
            var colors = new[]
            {
                new Color(0.1f, 0.8f, 1f, 1f),
                new Color(1f, 0.2f, 0.65f, 1f),
                new Color(1f, 0.85f, 0.2f, 1f),
                new Color(0.35f, 1f, 0.5f, 1f)
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var inside = x >= 4 && x < size - 4 && y >= 4 && y < size - 4;
                    var color = inside
                        ? colors[((x - 4) / 6 + ((y - 4) / 6)) % colors.Length]
                        : background;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
