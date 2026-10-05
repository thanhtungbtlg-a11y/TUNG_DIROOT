[CmdletBinding()]
param(
    [switch]$StaChild
)

$ErrorActionPreference = 'Stop'

if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) {
    $windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -STA -File $PSCommandPath -StaChild
    if ($LASTEXITCODE -ne 0) { throw "Responsive WPF layout regression failed with exit code $LASTEXITCODE." }
    return
}

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName WindowsBase

$repositoryRoot = Split-Path -Parent $PSScriptRoot

function Import-TestWindow {
    param([Parameter(Mandatory)][string]$RelativePath)

    $document = [Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    $document.Load((Join-Path $repositoryRoot $RelativePath))
    $document.DocumentElement.RemoveAttribute('Class', 'http://schemas.microsoft.com/winfx/2006/xaml')
    foreach ($attribute in @('Click', 'Drop', 'DragOver', 'PreviewDragOver', 'SelectionChanged', 'TextChanged')) {
        foreach ($node in @($document.SelectNodes("//*[@$attribute]"))) {
            $node.RemoveAttribute($attribute)
        }
    }

    $reader = [Xml.XmlNodeReader]::new($document)
    try { return [Windows.Markup.XamlReader]::Load($reader) }
    finally { $reader.Dispose() }
}

function Invoke-Layout {
    param(
        [Parameter(Mandatory)]$Window,
        [Parameter(Mandatory)][double]$Width,
        [Parameter(Mandatory)][double]$Height
    )

    $root = [Windows.FrameworkElement]$Window.Content
    $root.Measure([Windows.Size]::new($Width, $Height))
    $root.Arrange([Windows.Rect]::new(0, 0, $Width, $Height))
    $root.UpdateLayout()
}

function Assert-Inside {
    param(
        [Parameter(Mandatory)][Windows.FrameworkElement]$Child,
        [Parameter(Mandatory)][Windows.FrameworkElement]$Ancestor,
        [Parameter(Mandatory)][string]$Description
    )

    $origin = $Child.TransformToAncestor($Ancestor).Transform([Windows.Point]::new(0, 0))
    if ($origin.X -lt -0.5 -or $origin.Y -lt -0.5 -or
        ($origin.X + $Child.ActualWidth) -gt ($Ancestor.ActualWidth + 0.5) -or
        ($origin.Y + $Child.ActualHeight) -gt ($Ancestor.ActualHeight + 0.5)) {
        throw "$Description is clipped by its container."
    }
}

function Find-LogicalDescendant {
    param(
        [Parameter(Mandatory)]$Root,
        [Parameter(Mandatory)][Type]$Type
    )

    if ($Root -is $Type) { return $Root }
    foreach ($child in [Windows.LogicalTreeHelper]::GetChildren($Root)) {
        if ($child -isnot [Windows.DependencyObject]) { continue }
        $match = Find-LogicalDescendant -Root $child -Type $Type
        if ($null -ne $match) { return $match }
    }
    return $null
}

function Assert-TableGenLayout {
    param([double]$Width, [double]$Height, [bool]$ExpectWrap)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\TableGenWindow.xaml'
    Invoke-Layout $window $Width $Height

    $header = [Windows.FrameworkElement]$window.FindName('HeaderPanel')
    $title = [Windows.FrameworkElement]$window.FindName('TitlePanel')
    $toolbar = $window.FindName('MainToolbar')
    $linkToolbar = $window.FindName('LinkActionsToolbar')
    if ($null -eq $header -or $null -eq $title -or $null -eq $toolbar -or $null -eq $linkToolbar) {
        throw 'TableGen must expose HeaderPanel, TitlePanel, MainToolbar and LinkActionsToolbar.'
    }
    if ($toolbar -isnot [Windows.Controls.WrapPanel]) {
        throw 'TableGen MainToolbar must be a WrapPanel.'
    }

    $buttons = @($toolbar.Children | Where-Object { $_ -is [Windows.Controls.Button] })
    if ($buttons.Count -ne 11) { throw "Expected 11 TableGen toolbar buttons including explicit Rebuild; found $($buttons.Count)." }

    $titleBottom = $title.TransformToAncestor($header).Transform(
        [Windows.Point]::new(0, $title.ActualHeight)).Y
    $rows = [Collections.Generic.HashSet[int]]::new()
    foreach ($button in $buttons) {
        $origin = $button.TransformToAncestor($header).Transform([Windows.Point]::new(0, 0))
        if ($origin.Y -lt ($titleBottom - 0.5)) {
            throw "TableGen button '$($button.Content)' overlaps the title."
        }
        Assert-Inside $button $header "TableGen button '$($button.Content)'"
        [void]$rows.Add([Math]::Round($origin.Y))
    }

    if ($ExpectWrap -and $rows.Count -lt 2) { throw 'TableGen toolbar did not wrap at constrained width.' }
    if (-not $ExpectWrap -and $rows.Count -ne 1) { throw 'TableGen toolbar unexpectedly wrapped at wide width.' }

    if ($linkToolbar -isnot [Windows.Controls.WrapPanel]) {
        throw 'TableGen LinkActionsToolbar must be a WrapPanel.'
    }
    $linkButtons = @($linkToolbar.Children | Where-Object { $_ -is [Windows.Controls.Button] })
    if ($linkButtons.Count -ne 7) { throw "Expected 7 TableGen link action buttons; found $($linkButtons.Count)." }
    foreach ($button in $linkButtons) {
        Assert-Inside $button $linkToolbar "TableGen link action '$($button.Content)'"
    }
}

function Assert-AddExcelFooterLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\AddExcelWindow.xaml'
    $status = [Windows.Controls.TextBlock]$window.FindName('StatusText')
    $status.Text = 'Each parameter in the deserialization constructor must bind to an object property or field. This deliberately long status must not hide Import or Close.'
    Invoke-Layout $window $Width $Height

    $footer = [Windows.FrameworkElement]$window.FindName('FooterPanel')
    $buttons = [Windows.FrameworkElement]$window.FindName('FooterButtons')
    $import = [Windows.FrameworkElement]$window.FindName('ImportButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($null -eq $footer -or $null -eq $buttons -or $null -eq $import -or $null -eq $close) {
        throw 'Add Excel must expose FooterPanel, FooterButtons, ImportButton and CloseButton.'
    }

    Assert-Inside $buttons $footer 'Add Excel footer buttons'
    Assert-Inside $import $footer 'Add Excel Import button'
    Assert-Inside $close $footer 'Add Excel Close button'

    $statusRight = $status.TransformToAncestor($footer).Transform(
        [Windows.Point]::new($status.ActualWidth, 0)).X
    $buttonsLeft = $buttons.TransformToAncestor($footer).Transform([Windows.Point]::new(0, 0)).X
    if ($statusRight -gt ($buttonsLeft + 0.5)) {
        throw 'Add Excel status text overlaps the footer buttons.'
    }
}

function Assert-TableGenManagerColumnsAreSafeToClick {
    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\TableGenWindow.xaml'
    $grid = Find-LogicalDescendant -Root $window -Type ([Windows.Controls.DataGrid])
    if ($null -eq $grid) { throw 'TableGen must contain its links DataGrid.' }

    $checkColumns = @($grid.Columns | Where-Object { $_ -is [Windows.Controls.DataGridCheckBoxColumn] })
    $templateColumns = @($grid.Columns | Where-Object { $_ -is [Windows.Controls.DataGridTemplateColumn] })
    $textColumns = @($grid.Columns | Where-Object { $_ -is [Windows.Controls.DataGridTextColumn] })
    if ($checkColumns.Count -ne 0) { throw 'TableGen checkbox fields must not use two-click DataGridCheckBoxColumn controls.' }
    if ($templateColumns.Count -ne 2) { throw "Expected exactly 2 one-click TableGen checkbox templates; found $($templateColumns.Count)." }
    if ($textColumns.Count -ne 9) { throw "Expected exactly 9 read-only TableGen text columns; found $($textColumns.Count)." }

    $expectedCheckPaths = @('IsSelected', 'AutoSync')
    foreach ($column in $templateColumns) {
        $checkBox = $column.CellTemplate.LoadContent()
        if ($checkBox -isnot [Windows.Controls.CheckBox]) {
            throw "TableGen template column '$($column.Header)' must directly expose a one-click CheckBox."
        }
        $binding = [Windows.Data.BindingOperations]::GetBinding($checkBox, [Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty)
        $path = $binding.Path.Path
        if ($expectedCheckPaths -notcontains $path) {
            throw "Unexpected editable TableGen check column binding '$path'."
        }
        if ($binding.Mode -ne [Windows.Data.BindingMode]::TwoWay) {
            throw "TableGen check column '$path' must use an explicit TwoWay binding."
        }
    }

    foreach ($column in $textColumns) {
        $path = $column.Binding.Path.Path
        if (-not $column.IsReadOnly) {
            throw "TableGen text column '$path' can enter edit mode and crash on its getter-only property."
        }
        if ($column.Binding.Mode -ne [Windows.Data.BindingMode]::OneWay) {
            throw "TableGen text column '$path' must use an explicit OneWay binding."
        }
    }
}

function Assert-AddExcelSelectionControlsAreDirect {
    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\AddExcelWindow.xaml'
    $grid = Find-LogicalDescendant -Root $window -Type ([Windows.Controls.DataGrid])
    if ($null -eq $grid) { throw 'Add Excel must contain its import rows DataGrid.' }

    $legacyCheckColumns = @($grid.Columns | Where-Object { $_ -is [Windows.Controls.DataGridCheckBoxColumn] })
    $templateColumns = @($grid.Columns | Where-Object { $_ -is [Windows.Controls.DataGridTemplateColumn] })
    if ($legacyCheckColumns.Count -ne 0) { throw 'Add Excel checkbox fields must not require DataGrid edit mode.' }
    if ($templateColumns.Count -ne 2) { throw "Expected 2 one-click Add Excel checkbox templates; found $($templateColumns.Count)." }

    $expectedPaths = @('IsSelected', 'BlackAndWhite')
    foreach ($column in $templateColumns) {
        $checkBox = $column.CellTemplate.LoadContent()
        if ($checkBox -isnot [Windows.Controls.CheckBox]) {
            throw "Add Excel template column '$($column.Header)' must directly expose a CheckBox."
        }
        $binding = [Windows.Data.BindingOperations]::GetBinding($checkBox, [Windows.Controls.Primitives.ToggleButton]::IsCheckedProperty)
        if ($expectedPaths -notcontains $binding.Path.Path) {
            throw "Unexpected Add Excel checkbox binding '$($binding.Path.Path)'."
        }
        if ($binding.Mode -ne [Windows.Data.BindingMode]::TwoWay) {
            throw "Add Excel checkbox '$($binding.Path.Path)' must use TwoWay binding."
        }
    }

    $summary = [Windows.Controls.TextBlock]$window.FindName('SelectionSummaryText')
    $selectAll = [Windows.Controls.Button]$window.FindName('SelectAllButton')
    $clear = [Windows.Controls.Button]$window.FindName('ClearSelectionButton')
    $import = [Windows.Controls.Button]$window.FindName('ImportButton')
    $preview = [Windows.Controls.Button]$window.FindName('PreviewButton')
    if ($null -eq $summary -or $null -eq $selectAll -or $null -eq $clear -or $null -eq $preview) {
        throw 'Add Excel must expose preview, selection summary, Select All and Clear Selection controls.'
    }
    $summaryBinding = [Windows.Data.BindingOperations]::GetBinding($summary, [Windows.Controls.TextBlock]::TextProperty)
    $importBinding = [Windows.Data.BindingOperations]::GetBinding($import, [Windows.UIElement]::IsEnabledProperty)
    $previewBinding = [Windows.Data.BindingOperations]::GetBinding($preview, [Windows.UIElement]::IsEnabledProperty)
    if ($summaryBinding.Path.Path -ne 'SelectionSummary') { throw 'Selection summary must follow the Add Excel view model.' }
    if ($importBinding.Path.Path -ne 'CanImport') { throw 'Import availability must follow the current selection.' }
    if ($previewBinding.Path.Path -ne 'CanPreview') { throw 'Preview availability must follow the selected import row.' }
}

function Assert-WorkbookPreviewLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\WorkbookPreviewWindow.xaml'
    Invoke-Layout $window $Width $Height
    $image = [Windows.FrameworkElement]$window.FindName('PreviewImage')
    $issues = [Windows.Controls.DataGrid]$window.FindName('IssuesGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('PreviewFooterPanel')
    $close = [Windows.FrameworkElement]$window.FindName('PreviewCloseButton')
    if ($null -eq $image -or $null -eq $issues -or $null -eq $footer -or $null -eq $close) {
        throw 'Workbook Preview must expose image, diagnostics grid, footer and Close button.'
    }
    if ($issues.EnableRowVirtualization -ne $true) { throw 'Workbook Preview diagnostics must be virtualized.' }
    Assert-Inside $close $footer 'Workbook Preview Close button'
}

function Assert-TableGenProfileManagerLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\TableGenProfileManagerWindow.xaml'
    Invoke-Layout $window $Width $Height
    $toolbar = [Windows.FrameworkElement]$window.FindName('ProfileToolbar')
    $numbers = [Windows.FrameworkElement]$window.FindName('NumberOptionsPanel')
    $grid = [Windows.Controls.DataGrid]$window.FindName('MappingsGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('ProfileFooter')
    $save = [Windows.FrameworkElement]$window.FindName('SaveProfileButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseProfileButton')
    if ($null -eq $toolbar -or $null -eq $numbers -or $null -eq $grid -or $null -eq $footer -or $null -eq $save -or $null -eq $close) {
        throw 'TableGen Profile Manager must expose toolbar, number options, mapping grid and footer actions.'
    }
    if ($toolbar -isnot [Windows.Controls.WrapPanel] -or $numbers -isnot [Windows.Controls.WrapPanel]) {
        throw 'TableGen Profile Manager compact controls must wrap at constrained width.'
    }
    if ($grid.EnableRowVirtualization -ne $true) { throw 'TableGen Profile Manager rows must be virtualized.' }
    $legacyComboColumns = @($grid.Columns | Where-Object { $_ -is [Windows.Controls.DataGridComboBoxColumn] })
    $directComboColumns = @($grid.Columns | Where-Object { $_ -is [Windows.Controls.DataGridTemplateColumn] })
    if ($legacyComboColumns.Count -ne 0 -or $directComboColumns.Count -ne 2) {
        throw 'TableGen Profile Manager mappings must expose two one-click ComboBox template columns.'
    }
    Assert-Inside $save $footer 'TableGen Profile Manager Save button'
    Assert-Inside $close $footer 'TableGen Profile Manager Close button'
}

function Assert-TargetViewPickerLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\TargetViewPickerWindow.xaml'
    Invoke-Layout $window $Width $Height
    $combo = [Windows.FrameworkElement]$window.FindName('TargetViewBox')
    $buttons = [Windows.FrameworkElement]$window.FindName('PickerButtons')
    $move = [Windows.FrameworkElement]$window.FindName('MoveButton')
    $cancel = [Windows.FrameworkElement]$window.FindName('CancelButton')
    if ($null -eq $combo -or $null -eq $buttons -or $null -eq $move -or $null -eq $cancel) {
        throw 'Target View Picker must expose the target list and both footer actions.'
    }
    Assert-Inside $move $buttons 'Target View Picker Move button'
    Assert-Inside $cancel $buttons 'Target View Picker Cancel button'
}

function Assert-ParameterLoaderLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\ParameterLoaderWindow.xaml'
    Invoke-Layout $window $Width $Height
    if ($window.MinWidth -gt $window.Width -or $window.MinHeight -gt $window.Height) {
        throw 'Parameter Loader minimum size exceeds its preferred size.'
    }
    $grid = [Windows.Controls.DataGrid]$window.FindName('PlansGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('FooterPanel')
    $assign = [Windows.FrameworkElement]$window.FindName('AssignButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($null -eq $grid -or $null -eq $footer -or $null -eq $assign -or $null -eq $close) {
        throw 'Parameter Loader must expose PlansGrid, FooterPanel, AssignButton and CloseButton.'
    }
    if ($grid.ActualHeight -lt 120) { throw 'Parameter Loader result grid must receive star-sized vertical space.' }
    Assert-Inside $assign $footer 'Parameter Loader Assign button'
    Assert-Inside $close $footer 'Parameter Loader Close button'
}

function Assert-DataLinkLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\DataLinkWindow.xaml'
    Invoke-Layout $window $Width $Height
    if ($window.MinWidth -gt $window.Width -or $window.MinHeight -gt $window.Height) {
        throw 'DataLink minimum size exceeds its preferred size.'
    }
    $tabs = [Windows.Controls.TabControl]$window.FindName('WorkflowTabs')
    $grid = [Windows.Controls.DataGrid]$window.FindName('PreviewGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('FooterPanel')
    $apply = [Windows.FrameworkElement]$window.FindName('ApplyButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($null -eq $tabs -or $tabs.Items.Count -ne 3) { throw 'DataLink must expose exactly three workflow tabs.' }
    if ($null -eq $grid -or $null -eq $footer -or $null -eq $apply -or $null -eq $close) {
        throw 'DataLink must expose PreviewGrid, FooterPanel, ApplyButton and CloseButton.'
    }
    if ($grid.EnableRowVirtualization -ne $true) { throw 'DataLink preview rows must be virtualized.' }
    Assert-Inside $apply $footer 'DataLink Apply button'
    Assert-Inside $close $footer 'DataLink Close button'
}

function Assert-FilterLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\FilterWindow.xaml'
    Invoke-Layout $window $Width $Height
    if ($window.MinWidth -gt $window.Width -or $window.MinHeight -gt $window.Height) {
        throw 'Filter minimum size exceeds its preferred size.'
    }
    foreach ($name in @('ScopeSection', 'RulesSection', 'ResultsSection', 'ActionsSection')) {
        if ($null -eq $window.FindName($name)) { throw "Filter must expose the $name section." }
    }
    $grid = [Windows.Controls.DataGrid]$window.FindName('ResultsGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('FooterPanel')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($null -eq $grid -or $null -eq $footer -or $null -eq $close) {
        throw 'Filter must expose ResultsGrid, FooterPanel and CloseButton.'
    }
    if ($grid.EnableRowVirtualization -ne $true) { throw 'Filter result rows must be virtualized.' }
    Assert-Inside $close $footer 'Filter Close button'
}

function Assert-DocumentLinkLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\DocumentLinkWindow.xaml'
    Invoke-Layout $window $Width $Height
    $root = [Windows.FrameworkElement]$window.Content
    $import = [Windows.FrameworkElement]$window.FindName('ImportButton')
    $grid = [Windows.FrameworkElement]$window.FindName('LinksGrid')
    $status = [Windows.FrameworkElement]$window.FindName('StatusText')
    if ($null -eq $import -or $null -eq $grid -or $null -eq $status) {
        throw 'DocumentLink must expose ImportButton, LinksGrid and StatusText.'
    }
    Assert-Inside $import $root 'DocumentLink import button'
    Assert-Inside $grid $root 'DocumentLink links grid'
    Assert-Inside $status $root 'DocumentLink status text'
    if ($grid.ActualHeight -lt 120) { throw 'DocumentLink links grid is too short to manage existing links.' }
}

function Assert-BulkVgLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\BulkVgWindow.xaml'
    Invoke-Layout $window $Width $Height
    $root = [Windows.FrameworkElement]$window.Content
    $toolbar = $window.FindName('BulkVgToolbar')
    $targets = [Windows.FrameworkElement]$window.FindName('TargetsGrid')
    $items = [Windows.FrameworkElement]$window.FindName('ItemsGrid')
    $preview = [Windows.FrameworkElement]$window.FindName('PreviewGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('BulkVgFooter')
    $apply = [Windows.FrameworkElement]$window.FindName('ApplyButton')
    if ($toolbar -isnot [Windows.Controls.WrapPanel] -or $null -eq $targets -or $null -eq $items -or
        $null -eq $preview -or $null -eq $footer -or $null -eq $apply) {
        throw 'Bulk V/G must expose responsive toolbar, target/item/preview grids and footer.'
    }
    Assert-Inside $apply $footer 'Bulk V/G Apply button'
    Assert-Inside $targets $root 'Bulk V/G target grid'
    Assert-Inside $items $root 'Bulk V/G item grid'
    Assert-Inside $preview $root 'Bulk V/G preview grid'
    if ($targets.ActualHeight -lt 100 -or $items.ActualHeight -lt 100 -or $preview.ActualHeight -lt 80) {
        throw 'Bulk V/G grids do not receive enough star-sized space.'
    }
}

function Assert-SheetToolsLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\SheetToolsWindow.xaml'
    Invoke-Layout $window $Width $Height
    $root = [Windows.FrameworkElement]$window.Content
    $toolbar = $window.FindName('SheetToolsToolbar')
    $grid = [Windows.Controls.DataGrid]$window.FindName('PreviewGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('SheetToolsFooter')
    $apply = [Windows.FrameworkElement]$window.FindName('ApplyButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($toolbar -isnot [Windows.Controls.WrapPanel] -or $null -eq $grid -or $null -eq $footer -or
        $null -eq $apply -or $null -eq $close) {
        throw 'Sheet Tools must expose responsive toolbar, preview grid and footer actions.'
    }
    if ($grid.EnableRowVirtualization -ne $true) { throw 'Sheet Tools preview rows must be virtualized.' }
    Assert-Inside $toolbar $root 'Sheet Tools toolbar'
    Assert-Inside $grid $root 'Sheet Tools preview grid'
    Assert-Inside $apply $footer 'Sheet Tools Apply button'
    Assert-Inside $close $footer 'Sheet Tools Close button'
    if ($grid.ActualHeight -lt 140) { throw 'Sheet Tools preview grid does not receive enough star-sized space.' }
}

function Assert-ReOrderingLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\ReOrderingWindow.xaml'
    Invoke-Layout $window $Width $Height
    $root = [Windows.FrameworkElement]$window.Content
    $toolbar = $window.FindName('ReOrderingToolbar')
    $grid = [Windows.Controls.DataGrid]$window.FindName('PreviewGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('ReOrderingFooter')
    $apply = [Windows.FrameworkElement]$window.FindName('ApplyButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($toolbar -isnot [Windows.Controls.WrapPanel] -or $null -eq $grid -or $null -eq $footer -or
        $null -eq $apply -or $null -eq $close) {
        throw 'ReOrdering must expose responsive toolbar, preview grid and footer actions.'
    }
    if ($grid.EnableRowVirtualization -ne $true) { throw 'ReOrdering preview rows must be virtualized.' }
    Assert-Inside $toolbar $root 'ReOrdering toolbar'
    Assert-Inside $grid $root 'ReOrdering preview grid'
    Assert-Inside $apply $footer 'ReOrdering Apply button'
    Assert-Inside $close $footer 'ReOrdering Close button'
    if ($grid.ActualHeight -lt 140) { throw 'ReOrdering preview grid does not receive enough star-sized space.' }
}

function Assert-ParaManagerLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\ParaManagerWindow.xaml'
    Invoke-Layout $window $Width $Height
    $root = [Windows.FrameworkElement]$window.Content
    $toolbar = $window.FindName('ParaManagerToolbar')
    $grid = [Windows.Controls.DataGrid]$window.FindName('PreviewGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('ParaManagerFooter')
    $apply = [Windows.FrameworkElement]$window.FindName('ApplyButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($toolbar -isnot [Windows.Controls.WrapPanel] -or $null -eq $grid -or $null -eq $footer -or
        $null -eq $apply -or $null -eq $close) {
        throw 'ParaManager must expose responsive toolbar, preview grid and footer actions.'
    }
    if ($grid.EnableRowVirtualization -ne $true) { throw 'ParaManager preview rows must be virtualized.' }
    Assert-Inside $toolbar $root 'ParaManager toolbar'
    Assert-Inside $grid $root 'ParaManager preview grid'
    Assert-Inside $apply $footer 'ParaManager Apply button'
    Assert-Inside $close $footer 'ParaManager Close button'
    if ($grid.ActualHeight -lt 140) { throw 'ParaManager preview grid does not receive enough star-sized space.' }
}

function Assert-FamilyReviserLayout {
    param([double]$Width, [double]$Height)

    $window = Import-TestWindow 'src\TungDiroot.Addin\UI\FamilyReviserWindow.xaml'
    Invoke-Layout $window $Width $Height
    $root = [Windows.FrameworkElement]$window.Content
    $toolbar = $window.FindName('FamilyReviserToolbar')
    $grid = [Windows.Controls.DataGrid]$window.FindName('PreviewGrid')
    $footer = [Windows.FrameworkElement]$window.FindName('FamilyReviserFooter')
    $apply = [Windows.FrameworkElement]$window.FindName('ApplyButton')
    $close = [Windows.FrameworkElement]$window.FindName('CloseButton')
    if ($toolbar -isnot [Windows.Controls.WrapPanel] -or $null -eq $grid -or $null -eq $footer -or
        $null -eq $apply -or $null -eq $close) {
        throw 'FamilyReviser must expose responsive toolbar, preview grid and footer actions.'
    }
    if ($grid.EnableRowVirtualization -ne $true) { throw 'FamilyReviser preview rows must be virtualized.' }
    Assert-Inside $toolbar $root 'FamilyReviser toolbar'
    Assert-Inside $grid $root 'FamilyReviser preview grid'
    Assert-Inside $apply $footer 'FamilyReviser Apply button'
    Assert-Inside $close $footer 'FamilyReviser Close button'
    if ($grid.ActualHeight -lt 140) { throw 'FamilyReviser preview grid does not receive enough star-sized space.' }
}

Assert-TableGenLayout -Width 1760 -Height 900 -ExpectWrap $false
Assert-TableGenLayout -Width 760 -Height 520 -ExpectWrap $true
Assert-AddExcelFooterLayout -Width 760 -Height 520
Assert-TableGenManagerColumnsAreSafeToClick
Assert-AddExcelSelectionControlsAreDirect
Assert-WorkbookPreviewLayout -Width 1200 -Height 760
Assert-WorkbookPreviewLayout -Width 760 -Height 520
Assert-TableGenProfileManagerLayout -Width 1200 -Height 760
Assert-TableGenProfileManagerLayout -Width 760 -Height 520
Assert-TargetViewPickerLayout -Width 620 -Height 210
Assert-TargetViewPickerLayout -Width 520 -Height 190
Assert-ParameterLoaderLayout -Width 1100 -Height 650
Assert-ParameterLoaderLayout -Width 760 -Height 520
Assert-DataLinkLayout -Width 1100 -Height 700
Assert-DataLinkLayout -Width 760 -Height 520
Assert-FilterLayout -Width 1366 -Height 768
Assert-FilterLayout -Width 760 -Height 520
Assert-DocumentLinkLayout -Width 1320 -Height 820
Assert-DocumentLinkLayout -Width 780 -Height 560
Assert-BulkVgLayout -Width 1500 -Height 900
Assert-BulkVgLayout -Width 780 -Height 560
Assert-SheetToolsLayout -Width 1500 -Height 900
Assert-SheetToolsLayout -Width 780 -Height 560
Assert-ReOrderingLayout -Width 1300 -Height 820
Assert-ReOrderingLayout -Width 760 -Height 520
Assert-ParaManagerLayout -Width 1500 -Height 900
Assert-ParaManagerLayout -Width 800 -Height 580
Assert-FamilyReviserLayout -Width 1420 -Height 860
Assert-FamilyReviserLayout -Width 800 -Height 560

Write-Host 'Responsive WPF window layout regression passed.'
