﻿using ElysiaFramework;
using StickyHomeworks.Services;
using StickyHomeworks.ViewModels;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System;
using System.IO;

namespace StickyHomeworks.Views;

/// <summary>
/// HomeworkEditWindow.xaml 的交互逻辑
/// </summary>
public partial class HomeworkEditWindow : Window, INotifyPropertyChanged
{
    private RichTextBox _relatedRichTextBox = new();
    public MainWindow MainWindow { get; }
    public SettingsService SettingsService { get; }
    public ICommand AddImageCommand { get; }


    public HomeworkEditViewModel ViewModel { get; } = new();

    public bool IsOpened { get; set; } = false;

    public event EventHandler? EditingFinished;

    public event EventHandler? SubjectChanged;

    public void TryOpen()
    {
        if (IsOpened)
            return;
        Show();
        Activate();
        IsOpened = true;
    }

    public void TryClose()
    {
        if (!IsOpened)
            return;
        IsOpened = false;
        Hide();
    }


    public HomeworkEditWindow(MainWindow mainWindow, SettingsService settingsService)
    {
        MainWindow = mainWindow;
        SettingsService = settingsService;
        DataContext = this;
        InitializeComponent();

        AddImageCommand = new RelayCommand(AddImageToRichTextBox);

        ViewModel.PropertyChanged += ViewModelOnPropertyChanged;
        Loaded += HomeworkEditWindow_Loaded;
    }

    private void AddImageToRichTextBox()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择图片",
            Filter = "图片文件 (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            Multiselect = false
        };

        if (openFileDialog.ShowDialog() == true)
        {
            string filePath = openFileDialog.FileName;

            // 加载图片
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.UriSource = new Uri(filePath);
            bitmapImage.EndInit();

            // 创建 Image 元素
            Image image = new Image
            {
                Source = bitmapImage,
                Width = 100, // 可以根据需要调整宽度
                Height = 100 // 可以根据需要调整高度
            };

            // 创建 InlineUIContainer 并将 Image 添加到容器
            InlineUIContainer imageContainer = new InlineUIContainer(image);

            // 创建新的段落并添加 InlineUIContainer
            Paragraph paragraph = new Paragraph(imageContainer);

            // 将段落添加到 RichTextBox 的文档中
            RelatedRichTextBox.Document.Blocks.Add(paragraph);
        }
    }

    private void AddImageButton_Click(object sender, RoutedEventArgs e)
    {
        AddImageToRichTextBox();
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute();

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }


    private void HomeworkEditWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CenterWindowOnScreen();
    }



    public RichTextBox RelatedRichTextBox
    {
        get => _relatedRichTextBox;
        set
        {
            UnregisterOldTextBox(_relatedRichTextBox);
            RegisterNewTextBox(value);
            _relatedRichTextBox = value;
            OnPropertyChanged();
        }
    }

    protected override void OnInitialized(EventArgs e)
    {
        ViewModel.FontFamilies =
            new ObservableCollection<FontFamily>(from i in Fonts.SystemFontFamilies orderby i.ToString() select i)
                { (FontFamily)FindResource("MiSans") };
        base.OnInitialized(e);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel.IsRestoringSelection)
        {
            return;
        }
        var s = RelatedRichTextBox.Selection;
        switch (e.PropertyName)
        {
            case nameof(ViewModel.TextColor):
                s.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(ViewModel.TextColor));
                break;
            case nameof(ViewModel.Font):
                s.ApplyPropertyValue(TextElement.FontFamilyProperty, ViewModel.Font);
                break;
            case nameof(ViewModel.FontSize):
                s.ApplyPropertyValue(TextElement.FontSizeProperty, Math.Max(ViewModel.FontSize, 8));
                break;
        }
    }

    private void RegisterNewTextBox(RichTextBox richTextBox)
    {
        richTextBox.TextChanged += RichTextBoxOnTextChanged;
        richTextBox.SelectionChanged += RichTextBoxOnSelectionChanged;
    }

    private void RichTextBoxOnSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsRestoringSelection)
            return;
        Debug.WriteLine("selection changed!");
        if (RelatedRichTextBox.Selection.Start.Paragraph != null)
            ViewModel.SelectedParagraph = RelatedRichTextBox.Selection.Start.Paragraph;
        // Update selection
        var s = RelatedRichTextBox.Selection;
        if (!MainWindow.IsActive)
        {
            if (ViewModel is not { BeforeTextPointerStart: not null, BeforeTextPointerEnd: not null })
                return;
            ViewModel.IsRestoringSelection = true;
            RelatedRichTextBox.Selection.Select(ViewModel.BeforeTextPointerStart, ViewModel.BeforeTextPointerEnd);
            ViewModel.IsRestoringSelection = false;
            return;
        }
        ViewModel.Selection = s;
        ViewModel.BeforeTextPointerStart = s.Start;
        ViewModel.BeforeTextPointerEnd = s.End;
        ViewModel.IsRestoringSelection = true;
        Debug.WriteLine("selection updated!");
        var w = s.GetPropertyValue(TextElement.FontWeightProperty);
        if (w is FontWeight weight)
        {
            ViewModel.IsBold = weight >= FontWeights.Bold;
        }

        ViewModel.IsItalic = Equals(s.GetPropertyValue(TextElement.FontStyleProperty), FontStyles.Italic);
        if (s.GetPropertyValue(Paragraph.TextDecorationsProperty) is TextDecorationCollection decorations)
        {
            ViewModel.IsUnderlined = decorations.Contains(TextDecorations.Underline[0]);
            ViewModel.IsStrikeThrough = decorations.Contains(TextDecorations.Strikethrough[0]);
        }

        if (s.GetPropertyValue(TextElement.ForegroundProperty) is SolidColorBrush fg)
        {
            ViewModel.TextColor = fg.Color;
        }
        if (s.GetPropertyValue(TextElement.FontFamilyProperty) is FontFamily font)
        {
            ViewModel.Font = font;
        }

        if (s.GetPropertyValue(TextElement.FontSizeProperty) is double fontSize)
        {
            ViewModel.FontSize = fontSize;
        }
        ViewModel.IsRestoringSelection = false;
    }

    private void RichTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
    {
    }

    private void UnregisterOldTextBox(RichTextBox richTextBox)
    {
        richTextBox.TextChanged -= RichTextBoxOnTextChanged;
        richTextBox.SelectionChanged -= RichTextBoxOnSelectionChanged;
    }

    private void ListBoxTextStyles_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsRestoringSelection)
            return;
        var s = RelatedRichTextBox.Selection;
        s.ApplyPropertyValue(TextElement.FontWeightProperty, ViewModel.IsBold ? FontWeights.Bold : FontWeights.Regular);
        s.ApplyPropertyValue(TextElement.FontStyleProperty, ViewModel.IsItalic ? FontStyles.Italic : FontStyles.Normal);
        var decorations = new TextDecorationCollection();
        if (ViewModel.IsUnderlined)
            decorations.Add(TextDecorations.Underline);
        if (ViewModel.IsStrikeThrough)
            decorations.Add(TextDecorations.Strikethrough);
        s.ApplyPropertyValue(Paragraph.TextDecorationsProperty, decorations);
        RelatedRichTextBox.Focus();
    }

    private void ButtonClearColor_OnClick(object sender, RoutedEventArgs e)
    {
        var s = RelatedRichTextBox.Selection;
        s.ApplyPropertyValue(TextElement.ForegroundProperty, GetValue(TextElement.ForegroundProperty));
    }



    private void ButtonFontSizeDecrease_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.FontSize -= 2;
    }

    private void ButtonFontSizeIncrease_OnClick(object sender, RoutedEventArgs e)
    {
        ViewModel.FontSize += 2;
    }

    private void ButtonEditingDone_OnClick(object sender, RoutedEventArgs e)
    {
        EditingFinished?.Invoke(this, EventArgs.Empty);
        BackupSettingsJson();
        AppEx.GetService<ProfileService>().SaveProfile();
    }

    private void BackupSettingsJson()
    {
        string sourceFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.json");
        string backupDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
        string backupFilePath = Path.Combine(backupDirectory, $"Settings_{DateTime.Now:yyyyMMddHHmmss}.json");

        try
        {
            // 确保源文件存在
            if (File.Exists(sourceFilePath))
            {
                // 如果Backups文件夹不存在，则创建它
                if (!Directory.Exists(backupDirectory))
                {
                    Directory.CreateDirectory(backupDirectory);
                }

                // 复制文件到新的文件名
                File.Copy(sourceFilePath, backupFilePath, true);
                MessageBox.Show("Settings.json has been backed up successfully.");
            }
            else
            {
                MessageBox.Show("Settings.json file does not exist.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error backing up Settings.json: {ex.Message}");
        }
    }

    private void Selector_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SubjectChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ButtonAddToColor_OnClick(object sender, RoutedEventArgs e)
    {
        if (SettingsService.Settings.SavedColors.Contains(ViewModel.TextColor))
            return;
        SettingsService.Settings.SavedColors.Insert(0, ViewModel.TextColor);
        while (SettingsService.Settings.SavedColors.Count > 6)
        {
            SettingsService.Settings.SavedColors.RemoveAt(6);
        }
        SettingsService.SaveSettings();
    }

    private void ListBoxColors_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsUpdatingColor)
            return;
        ViewModel.IsUpdatingColor = true;
        foreach (var i in e.AddedItems)
        {
            if (i is Color c)
                ViewModel.TextColor = c;
        }

        if (sender is ListBox l)
            l.SelectedIndex = -1;
        ViewModel.IsUpdatingColor = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public void ShowAtMousePosition()
    {
        // 获取鼠标位置
        var mousePosition = System.Windows.Forms.Control.MousePosition;

        // 设置窗口位置为鼠标位置的右侧
        Left = mousePosition.X + 10; // 向右偏移10个像素
        Top = mousePosition.Y;

        // 确保窗口在屏幕内
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // 确保窗口在屏幕内
        if (Left < 0) Left = 0;
        if (Top < 0) Top = 0;
        if (Left + ActualWidth > screenWidth) Left = screenWidth - ActualWidth;
        if (Top + ActualHeight > screenHeight) Top = screenHeight - ActualHeight;

        // 显示窗口
        Show();
    IsOpened = true; // 设置窗口状态为已打开
    }



    private void CenterWindowOnScreen()
    {
        var screenWidth = SystemParameters.PrimaryScreenWidth;
        var screenHeight = SystemParameters.PrimaryScreenHeight;

        // 计算窗口的中心位置
        Left = (screenWidth - ActualWidth) / 2;
        Top = (screenHeight - ActualHeight) / 2;

        // 确保窗口在屏幕内
        if (Left < 0) Left = 0;
        if (Top < 0) Top = 0;
        if (Left + ActualWidth > screenWidth) Left = screenWidth - ActualWidth;
        if (Top + ActualHeight > screenHeight) Top = screenHeight - ActualHeight;
    }
}