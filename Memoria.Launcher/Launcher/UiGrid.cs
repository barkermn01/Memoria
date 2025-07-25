﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
namespace Memoria.Launcher
{
    public class UiGrid : Grid
    {
        public UiGrid()
        {
            SetCols(100);
            Width = 293;
            Margin = new Thickness(0);
        }

        public SolidColorBrush TextColor = Brushes.White;
        public Thickness CommonMargin = new Thickness(0, 3, 0, 3);
        public Int32 Row = -1;
        public Int32 MaxColumns = 100;
        public Int32 FontWeightNormal = 400;
        public Int32 FontWeightCombobox = 500;
        public Int32 FontSizeNormal = 14;
        public Int32 ComboboxHeight = 22;
        public Int32 TooltipDisplayDelay = 1;
        public void SetRows(Int32 count)
        {
            count -= RowDefinitions.Count;
            if (count > 1) while (count-- > 0) RowDefinitions.Add(new RowDefinition());
        }
        public void SetCols(Int32 count)
        {
            count = 100;
            count -= ColumnDefinitions.Count;
            if (count > 1) while (count-- > 0) ColumnDefinitions.Add(new ColumnDefinition());
        }
        public void SetRowsHeight(GridLength height)
        {
            foreach (RowDefinition row in RowDefinitions)
                row.Height = height;
        }
        public T AddUiElement<T>(T uiElement, Int32 row, Int32 col, Int32 rowSpan = 0, Int32 colSpan = 0) where T : UIElement
        {
            if (row > 0) uiElement.SetValue(RowProperty, row);
            if (col > 0) uiElement.SetValue(ColumnProperty, col);
            if (rowSpan > 0) uiElement.SetValue(RowSpanProperty, rowSpan);
            if (colSpan > 0) uiElement.SetValue(ColumnSpanProperty, colSpan);

            Children.Add(uiElement);
            return uiElement;
        }

        private static HashSet<string> GetImageResourceNames()
        {
            var imageNames = new HashSet<string>();
            var assembly = Assembly.GetExecutingAssembly();

            using (var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().FirstOrDefault(name => name.EndsWith(".g.resources"))))
            using (var reader = new ResourceReader(stream))
            {
                foreach (DictionaryEntry entry in reader)
                {
                    var resourceKey = entry.Key.ToString();

                    if (resourceKey.StartsWith("images/"))
                        imageNames.Add(Path.GetFileName(resourceKey));
                }
            }

            return imageNames;
        }
        private static HashSet<string> ImageResourceNames = GetImageResourceNames();

        public static void MakeTooltip(FrameworkElement uiElement, String text = "", String imageName = "", String curstorType = "mog", PlacementMode placement = PlacementMode.Bottom)
        {
            if (text != "" || imageName != "")
            {
                StackPanel tooltipStackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(0)
                };
                try
                {
                    Image helpIcon = new Image
                    {
                        Source = new BitmapImage(new Uri("pack://application:,,,/images/helpicon.png")),
                        MaxWidth = 28,
                        Opacity = 1,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 5, 0, 0)
                    };
                    tooltipStackPanel.Children.Add(helpIcon);
                }
                catch { }

                TextBlock tooltipTextBlock = null;
                if (text != "")
                {
                    DropShadowEffect dropShadow = new DropShadowEffect
                    {
                        Color = (Color)ColorConverter.ConvertFromString("#7f93a8"),  // Your specific color
                        BlurRadius = 0,
                        ShadowDepth = 1,
                        Direction = 320,
                        Opacity = 1
                    };
                    tooltipTextBlock = new TextBlock
                    {
                        Opacity = 1,
                        MaxWidth = 275,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Effect = dropShadow,
                        Margin = new Thickness(0)
                    };
                    if (Lang.Res.Contains(text))
                    {
                        tooltipTextBlock.SetResourceReference(TextBlock.TextProperty, text);
                    }
                    else
                    {
                        tooltipTextBlock.Text = text;
                    }
                    tooltipStackPanel.Children.Add(tooltipTextBlock);
                }

                if (imageName != "")
                {
                    try
                    {
                        String imagePath = null;
                        if (ImageResourceNames.Contains(imageName))
                            imagePath = "pack://application:,,,/images/" + imageName;
                        else if (imageName.StartsWith("http"))
                            imagePath = imageName;
                        else if (File.Exists(imageName))
                            imagePath = Path.GetFullPath(imageName);

                        if (imagePath != null)
                        {
                            Image tooltipImage = new Image
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness(0, 3, 0, 0),

                            };
                            tooltipImage.Loaded += (s, e) =>
                            {
                                // DPI can mess with the size so we force the actual pixel size
                                tooltipImage.Width = (tooltipImage.Source as BitmapSource).PixelWidth;
                                tooltipImage.Height = (tooltipImage.Source as BitmapSource).PixelHeight;

                                if (tooltipImage.Width > 550)
                                {
                                    tooltipImage.Height = Math.Floor(550 * tooltipImage.Height / tooltipImage.Width);
                                    tooltipImage.Width = 550;
                                }

                                if (tooltipImage.Height > 300)
                                {
                                    tooltipImage.Width = Math.Floor(300 * tooltipImage.Width / tooltipImage.Height);
                                    tooltipImage.Height = 300;
                                }

                                if (tooltipTextBlock != null && tooltipImage.Width > 275)
                                    tooltipTextBlock.MaxWidth = tooltipImage.Width;
                            };
                            BitmapImage bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(imagePath);
                            bitmap.EndInit();
                            tooltipImage.Source = bitmap;
                            RenderOptions.SetBitmapScalingMode(tooltipImage, BitmapScalingMode.HighQuality);
                            tooltipStackPanel.Children.Add(tooltipImage);
                        }
                    }
                    catch { }
                }

                Border bottomrightBorder = new Border
                {
                    BorderThickness = new Thickness(0, 0, 2, 2),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#487870")),
                    Padding = new Thickness(5, 0, 5, 5),
                    CornerRadius = new CornerRadius(7, 0, 7, 0),
                    Child = tooltipStackPanel
                };
                Border topleftBorder = new Border
                {
                    BorderThickness = new Thickness(2, 2, 0, 0),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#98c8b8")),
                    CornerRadius = new CornerRadius(7, 0, 7, 0),
                    Background = Brushes.Transparent,
                    Child = bottomrightBorder
                };
                Border outerBorder = new Border
                {
                    BorderThickness = new Thickness(2, 2, 2, 2),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202830")),
                    CornerRadius = new CornerRadius(7, 0, 7, 0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#78b0a7")),
                    Child = topleftBorder
                };
                ToolTip toolTip = new ToolTip
                {
                    Content = outerBorder,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383840")),
                    Background = Brushes.Transparent,
                    HasDropShadow = false,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0, 0, 5, 0),
                    Margin = new Thickness(0),
                    PlacementTarget = uiElement,
                    Placement = placement,
                    VerticalOffset = 0,
                    HorizontalOffset = 0,
                    ForceCursor = true,
                    Opacity = 1
                };
                if (uiElement.ToolTip == null || uiElement.ToolTip is string)
                {
                    uiElement.ToolTipOpening += (object sender, ToolTipEventArgs e) =>
                    {
                        // Allegedly fixes the System.ArgumentNullException within System.Windows.Controls.ToolTipService.GetBetweenShowDelay
                        e.Handled = true;
                    };
                    uiElement.MouseEnter += (sender, e) =>
                    {
                        if (uiElement.ToolTip is ToolTip)
                        {
                            ((ToolTip)uiElement.ToolTip).IsOpen = true;
                            if (uiElement.IsMouseOver)
                            {
                                if (curstorType == "mog")
                                    Mouse.OverrideCursor = new Cursor(Application.GetResourceStream(new Uri("pack://application:,,,/images/moogle.cur")).Stream);
                                else if (curstorType == "hand")
                                    Mouse.OverrideCursor = new Cursor(Application.GetResourceStream(new Uri("pack://application:,,,/images/cursorHand.cur")).Stream);
                            }
                        }
                    };
                    uiElement.MouseLeave += (sender, e) =>
                    {
                        if (uiElement.ToolTip is ToolTip)
                        {
                            ((ToolTip)uiElement.ToolTip).IsOpen = false; // Force close the tooltip when the mouse leaves the element
                            if (!uiElement.IsMouseOver) Mouse.OverrideCursor = null;
                        }
                    };
                }
                ToolTipService.SetToolTip(uiElement, toolTip);
                ToolTipService.SetInitialShowDelay(uiElement, 0);
            }
        }
        public void MakeFontPreview(ComboBox uiElement)
        {

            StackPanel tooltipStackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(10)
            };

            DropShadowEffect dropShadow = new DropShadowEffect
            {
                Color = (Color)ColorConverter.ConvertFromString("#707070"),  // This color is in linear space for some reason
                BlurRadius = 0,
                ShadowDepth = 5,
                Direction = 315,
                Opacity = 1
            };
            TextBlock tooltipTextBlock = new TextBlock
            {
                Text = "The quick brown fox jumps over the lazy dog",
                Opacity = 1,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8C8C8")),
                MaxWidth = 275,
                FontSize = 24,
                TextWrapping = TextWrapping.Wrap,
                Effect = dropShadow,
                Margin = new Thickness(0)
            };
            tooltipStackPanel.Children.Add(tooltipTextBlock);

            Border bottomrightBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 2, 2),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383840")),
                CornerRadius = new CornerRadius(7, 0, 7, 0),
                Child = tooltipStackPanel
            };
            Border topleftBorder = new Border
            {
                BorderThickness = new Thickness(2, 2, 0, 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#707878")),
                CornerRadius = new CornerRadius(7, 0, 7, 0),
                Background = Brushes.Transparent,
                Child = bottomrightBorder
            };
            Border outerBorder = new Border
            {
                BorderThickness = new Thickness(2, 2, 2, 2),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202830")),
                CornerRadius = new CornerRadius(7, 0, 7, 0),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#595959")),
                Child = topleftBorder
            };
            ToolTip toolTip = new ToolTip
            {
                Content = outerBorder,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#383840")),
                Background = Brushes.Transparent,
                HasDropShadow = false,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                PlacementTarget = uiElement,
                Placement = PlacementMode.Bottom,
                VerticalOffset = 0,
                HorizontalOffset = 0,
                ForceCursor = true,
                Opacity = 1
            };
            uiElement.ToolTipOpening += (object sender, ToolTipEventArgs e) =>
            {
                // Allegedly fixes the System.ArgumentNullException within System.Windows.Controls.ToolTipService.GetBetweenShowDelay
                e.Handled = true;
            };
            uiElement.MouseEnter += (sender, e) =>
            {
                if (uiElement.SelectedValue != null && !uiElement.SelectedValue.ToString().StartsWith("Final Fantasy IX"))
                {
                    ToolTipService.SetToolTip(uiElement, toolTip);
                    ToolTipService.SetInitialShowDelay(uiElement, 0);
                    toolTip.IsOpen = true;
                }
            };
            uiElement.SelectionChanged += (sender, e) =>
            {
                if (!uiElement.SelectedValue.ToString().StartsWith("Final Fantasy IX"))
                {
                    String fontFamily = uiElement.SelectedValue.ToString().Replace("Bold", "").Replace("Italic", "");
                    Boolean bold = uiElement.SelectedValue.ToString().Contains("Bold");
                    Boolean italic = uiElement.SelectedValue.ToString().Contains("Italic");
                    tooltipTextBlock.FontFamily = new FontFamily(fontFamily);
                    tooltipTextBlock.FontStyle = italic ? FontStyles.Italic : FontStyles.Normal;
                    tooltipTextBlock.FontWeight = bold ? FontWeights.Bold : FontWeights.Normal;
                    ToolTipService.SetToolTip(uiElement, toolTip);
                    ToolTipService.SetInitialShowDelay(uiElement, 0);
                    toolTip.IsOpen = uiElement.IsMouseOver;
                }
                else
                {
                    toolTip.IsOpen = false;
                    ToolTipService.SetToolTip(uiElement, null);
                }
            };
            uiElement.MouseLeave += (sender, e) =>
            {
                toolTip.IsOpen = false; // Force close the tooltip when the mouse leaves the element
                ToolTipService.SetToolTip(uiElement, null);
            };
        }
        public void CreateCheckbox(String property, String text, String tooltip = "", Int32 firstColumn = 0, String propertyToEnable = "", String tooltipImage = "")
        {
            if (firstColumn == 0)
            {
                Row++;
                RowDefinitions.Add(new RowDefinition());
            }
            CheckBox checkBox = new CheckBox();
            if (Lang.Res.Contains(text))
            {
                checkBox.SetResourceReference(ContentControl.ContentProperty, text);
            }
            else
            {
                checkBox.Content = text;
            }
            checkBox.IsChecked = null;
            MakeTooltip(checkBox, tooltip, tooltipImage);
            checkBox.Foreground = TextColor;
            checkBox.FontWeight = FontWeight.FromOpenTypeWeight(FontWeightNormal);
            checkBox.FontSize = FontSizeNormal;
            checkBox.SetBinding(ToggleButton.IsCheckedProperty, new Binding(property) { Mode = BindingMode.TwoWay });
            if (propertyToEnable != "")
                checkBox.SetBinding(ToggleButton.IsEnabledProperty, new Binding(propertyToEnable) { Mode = BindingMode.TwoWay });
            checkBox.VerticalContentAlignment = VerticalAlignment.Center;
            checkBox.Margin = new Thickness(0);
            checkBox.Height = 28;
            checkBox.Style = (Style)Application.Current.FindResource("ToggleStyle");
            checkBox.SetValue(RowProperty, Row);
            checkBox.SetValue(ColumnProperty, firstColumn);
            checkBox.SetValue(RowSpanProperty, 1);
            checkBox.SetValue(ColumnSpanProperty, MaxColumns);
            Children.Add(checkBox);
        }
        public void CreateTextbloc(String text, String tooltip = "", String tooltipImage = "", Int32 columns = 100)
        {
            Row++;
            RowDefinitions.Add(new RowDefinition());
            TextBlock textbloc = new TextBlock();
            if (Lang.Res.Contains(text))
            {
                textbloc.SetResourceReference(TextBlock.TextProperty, text);
            }
            else
            {
                textbloc.Text = text;
            }
            MakeTooltip(textbloc, tooltip, tooltipImage);
            textbloc.Foreground = TextColor;
            Border border = new Border();
            textbloc.FontSize = FontSizeNormal;
            textbloc.VerticalAlignment = VerticalAlignment.Center;
            textbloc.Margin = new Thickness(0);
            textbloc.Padding = new Thickness(0, 4, 0, 6);
            border.VerticalAlignment = VerticalAlignment.Center;
            border.SetValue(RowProperty, Row);
            border.SetValue(ColumnProperty, 0);
            border.SetValue(RowSpanProperty, 1);
            border.SetValue(ColumnSpanProperty, columns);
            border.Child = textbloc;
            Children.Add(border);
        }

        public void CreateHeading(String text)
        {
            Row++;
            RowDefinitions.Add(new RowDefinition());
            TextBlock textbloc = new TextBlock();
            if (Lang.Res.Contains(text))
            {
                textbloc.SetResourceReference(TextBlock.TextProperty, text);
            }
            else
            {
                textbloc.Text = text;
            }
            textbloc.Foreground = TextColor;
            textbloc.TextAlignment = TextAlignment.Center;
            textbloc.HorizontalAlignment = HorizontalAlignment.Center;
            textbloc.FontStretch = FontStretch.FromOpenTypeStretch(9);
            textbloc.FontWeight = FontWeight.FromOpenTypeWeight(500);
            textbloc.FontSize = 14;
            textbloc.VerticalAlignment = VerticalAlignment.Center;
            textbloc.Margin = new Thickness(0);
            textbloc.Height = 18;
            textbloc.Padding = new Thickness(0);
            textbloc.SetValue(TextBlock.FontFamilyProperty, Application.Current.FindResource("CenturyGothic") as FontFamily);
            Border border = new Border();
            border.SetValue(RowProperty, Row);
            border.SetValue(ColumnProperty, 0);
            border.SetValue(RowSpanProperty, 1);
            border.SetValue(ColumnSpanProperty, 100);
            //border.Background = (SolidColorBrush)Application.Current.FindResource("BrushAccentColor");
            border.SetResourceReference(Border.BackgroundProperty, "BrushAccentColor");
            //border.Opacity = 0.8;
            border.CornerRadius = new CornerRadius(5);
            border.Margin = new Thickness(0, 7, 0, 3);
            border.Height = 20;
            border.HorizontalAlignment = HorizontalAlignment.Stretch;
            border.VerticalAlignment = VerticalAlignment.Center;
            border.Child = textbloc;
            Children.Add(border);
        }

        private static Dictionary<ComboBox, List<String>> _comboBoxes = new Dictionary<ComboBox, List<String>>();
        public static void RefereshComboBoxes()
        {
            foreach (ComboBox comboBox in _comboBoxes.Keys)
            {
                Int32 i = comboBox.SelectedIndex;
                List<String> choices = new List<String>();
                foreach (String value in _comboBoxes[comboBox])
                {
                    if (Lang.Res.Contains(value))
                        choices.Add((String)Lang.Res[value]);
                    else
                        choices.Add(value);
                }
                comboBox.ItemsSource = choices;
                comboBox.SelectedIndex = i;
            }
        }

        public ComboBox CreateCombobox(String property, IEnumerable<String> options, Int32 firstColumn = 50, String text = "", String tooltip = "", String tooltipImage = "", Boolean selectByName = false)
        {
            if (text != "")
            {
                CreateTextbloc(text, tooltip, tooltipImage, firstColumn);
            }
            else if (firstColumn == 0)
            {
                Row++;
                RowDefinitions.Add(new RowDefinition());
            }

            List<String> choices = new List<String>();
            foreach (String value in options)
            {
                if (Lang.Res.Contains(value))
                    choices.Add((String)Lang.Res[value]);
                else
                    choices.Add(value);
            }

            ComboBox comboBox = new ComboBox();
            comboBox.ItemsSource = choices;
            //MakeTooltip(comboBox, tooltip, tooltipImage);
            if (property == "FontChoice")
                MakeFontPreview(comboBox);
            comboBox.Margin = CommonMargin;
            comboBox.Height = ComboboxHeight;
            if (selectByName)
                comboBox.SetBinding(Selector.SelectedItemProperty, new Binding(property) { Mode = BindingMode.TwoWay });
            else
                comboBox.SetBinding(Selector.SelectedIndexProperty, new Binding(property) { Mode = BindingMode.TwoWay });
            comboBox.SetValue(RowProperty, Row);
            comboBox.SetValue(ColumnProperty, firstColumn);
            comboBox.SetValue(RowSpanProperty, 1);
            comboBox.SetValue(ColumnSpanProperty, MaxColumns - firstColumn);
            comboBox.MouseEnter += (sender, e) =>
            {
                comboBox.Focus();
            };
            Children.Add(comboBox);

            _comboBoxes[comboBox] = new List<string>(options);
            return comboBox;
        }
        public void CreateSlider(String indexproperty, String sliderproperty, double min, double max, double tickFrequency, String stringFormat = "", Int32 firstColumn = 0, String text = "", String tooltip = "", String tooltipImage = "")
        {
            if (text != "" && firstColumn > 0)
            {
                CreateTextbloc(text, tooltip, tooltipImage, firstColumn - 2);
            }
            else if (firstColumn == 0)
            {
                Row++;
                RowDefinitions.Add(new RowDefinition());
            }
            Slider slider = new Slider();
            slider.Value = 0;
            slider.SetBinding(Slider.ValueProperty, new Binding(sliderproperty) { Mode = BindingMode.TwoWay });
            slider.VerticalContentAlignment = VerticalAlignment.Center;
            slider.VerticalAlignment = VerticalAlignment.Center;
            slider.Margin = new Thickness(0);
            slider.Minimum = min;
            slider.Maximum = max;
            slider.TickFrequency = tickFrequency;
            slider.IsSnapToTickEnabled = true;
            slider.TickPlacement = TickPlacement.None;
            slider.SetValue(RowProperty, Row);
            slider.SetValue(ColumnProperty, firstColumn + 6);
            slider.SetValue(RowSpanProperty, 1);
            slider.SetValue(ColumnSpanProperty, (MaxColumns - firstColumn));
            slider.MouseWheel += (sender, e) =>
            {
                slider.Value = Math.Max(Math.Min(slider.Value + Math.Sign(e.Delta) * tickFrequency, max), min);
            };
            Children.Add(slider);

            TextBlock textbloc = new TextBlock();
            textbloc.Text = "";
            if (stringFormat == "") stringFormat = "{0}";
            textbloc.SetBinding(TextBlock.TextProperty, new Binding(indexproperty) { Mode = BindingMode.TwoWay, StringFormat = stringFormat });
            textbloc.Foreground = TextColor;
            //textbloc.Background = Brushes.Black;
            textbloc.Opacity = 0.6;
            textbloc.FontWeight = FontWeight.FromOpenTypeWeight(FontWeightNormal);
            textbloc.FontSize = FontSizeNormal;
            textbloc.HorizontalAlignment = HorizontalAlignment.Right;
            textbloc.TextAlignment = TextAlignment.Right;
            textbloc.VerticalAlignment = VerticalAlignment.Center;
            textbloc.Margin = new Thickness(0);
            textbloc.Padding = new Thickness(0, 4, 0, 6);
            textbloc.Height = 28;
            textbloc.SetValue(RowProperty, Row);
            textbloc.SetValue(ColumnProperty, firstColumn - 8);
            textbloc.SetValue(RowSpanProperty, 1);
            textbloc.SetValue(ColumnSpanProperty, 13);
            Children.Add(textbloc);
        }
    }
}
