using System;
using System.Collections.Generic;
using System.Windows;
using TungDiroot.Core.Links;

namespace TungDiroot.Addin.UI;

public partial class TargetViewPickerWindow : Window
{
    public TargetViewPickerWindow(IReadOnlyList<LinkTargetViewOption> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        InitializeComponent();
        TargetViewBox.ItemsSource = options;
        if (options.Count > 0) TargetViewBox.SelectedIndex = 0;
    }

    public LinkTargetViewOption? SelectedTarget => TargetViewBox.SelectedItem as LinkTargetViewOption;

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedTarget == null) return;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
