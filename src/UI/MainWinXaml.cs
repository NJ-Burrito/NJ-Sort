// NJ-Sort: the main window's layout and styles, as one XAML string. ~TOKENS~ are filled from Theme.cs (see its header for the scales).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using W = System.Windows;
using WC = System.Windows.Controls;
using WM = System.Windows.Media;
using WA = System.Windows.Media.Animation;
using WI = System.Windows.Input;
using WT = System.Windows.Threading;
using WIn = System.Windows.Interop;
using F = System.Windows.Forms;
using D = System.Drawing;

partial class MainWin {
    const string Xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='NJ-Sort' Width='1040' SizeToContent='Height' ResizeMode='CanMinimize' WindowStartupLocation='CenterScreen'
        Background='~BG~' Foreground='~FG~' FontFamily='Thmanyah Sans, Segoe UI' FontSize='13' UseLayoutRounding='True' TextOptions.TextFormattingMode='Ideal'>
  <Window.Resources>
    <SolidColorBrush x:Key='Muted' Color='~MUTED~'/>
    <Style x:Key='Clear' TargetType='RepeatButton'>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style x:Key='Fill' TargetType='RepeatButton'>
      <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RepeatButton'><Border Height='4' CornerRadius='2' Background='~ACC~' VerticalAlignment='Center'/></ControlTemplate></Setter.Value></Setter>
    </Style>
    <Style TargetType='ScrollBar'>
      <Setter Property='Width' Value='10'/><Setter Property='MinWidth' Value='10'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Track x:Name='PART_Track' IsDirectionReversed='True'>
            <Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Style='{StaticResource Clear}'/></Track.DecreaseRepeatButton>
            <Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Style='{StaticResource Clear}'/></Track.IncreaseRepeatButton>
            <Track.Thumb><Thumb Cursor='Hand'><Thumb.Template><ControlTemplate TargetType='Thumb'>
              <Border x:Name='T' CornerRadius='3' Margin='3,0,1,0' Background='~LINE2~'/>
              <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='T' Property='Background' Value='~DIM~'/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Card' TargetType='Border'>
      <Setter Property='Background' Value='~SURFACE~'/><Setter Property='BorderBrush' Value='~LINE~'/><Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='12'/><Setter Property='Padding' Value='20'/>
    </Style>
    <Style x:Key='H' TargetType='TextBlock'><Setter Property='FontSize' Value='16'/><Setter Property='FontWeight' Value='Bold'/></Style>
    <Style x:Key='Sub' TargetType='TextBlock'><Setter Property='Foreground' Value='~MUTED~'/><Setter Property='FontSize' Value='12'/><Setter Property='TextWrapping' Value='Wrap'/><Setter Property='LineHeight' Value='18'/></Style>
    <Style x:Key='Rule' TargetType='Border'><Setter Property='Height' Value='1'/><Setter Property='Background' Value='~LINE~'/></Style>
    <Style x:Key='Btn' TargetType='Button'>
      <Setter Property='Foreground' Value='~FG~'/><Setter Property='Background' Value='~RAISED~'/><Setter Property='BorderBrush' Value='~LINE2~'/>
      <Setter Property='Padding' Value='16,0'/><Setter Property='MinHeight' Value='40'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='14'/>
      <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8' Padding='{TemplateBinding Padding}' RenderTransformOrigin='0.5,0.5'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.88'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='RenderTransform'><Setter.Value><ScaleTransform ScaleX='0.97' ScaleY='0.97'/></Setter.Value></Setter></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='~FG~'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Background' Value='~ACC~'/><Setter Property='BorderBrush' Value='~ACC~'/><Setter Property='Foreground' Value='~INK~'/><Setter Property='FontWeight' Value='Bold'/>
    </Style>
    <Style x:Key='Ghost' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Background' Value='Transparent'/><Setter Property='BorderBrush' Value='~LINE~'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='8' Padding='{TemplateBinding Padding}' RenderTransformOrigin='0.5,0.5'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='~RAISED~'/><Setter TargetName='B' Property='BorderBrush' Value='~LINE2~'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='RenderTransform'><Setter.Value><ScaleTransform ScaleX='0.97' ScaleY='0.97'/></Setter.Value></Setter></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='~FG~'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Danger' TargetType='Button' BasedOn='{StaticResource Ghost}'>
      <Setter Property='Foreground' Value='~CRIT~'/><Setter Property='BorderBrush' Value='Transparent'/>
    </Style>
    <Style x:Key='IconBtn' TargetType='Button' BasedOn='{StaticResource Ghost}'>
      <Setter Property='Padding' Value='0'/><Setter Property='Width' Value='40'/><Setter Property='BorderBrush' Value='Transparent'/><Setter Property='Foreground' Value='~MUTED~'/><Setter Property='FontSize' Value='18'/>
    </Style>
    <Style x:Key='Field' TargetType='TextBox'>
      <Setter Property='Foreground' Value='~FG~'/><Setter Property='CaretBrush' Value='~FG~'/><Setter Property='FontSize' Value='14'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='B' Background='~FIELD~' BorderBrush='~LINE~' BorderThickness='1' CornerRadius='8' Padding='12,0' MinHeight='40'>
            <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='~LINE2~'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='~ACC~'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='HkBox' TargetType='TextBox' BasedOn='{StaticResource Field}'>
      <Setter Property='IsReadOnly' Value='True'/><Setter Property='IsReadOnlyCaretVisible' Value='False'/><Setter Property='Cursor' Value='Hand'/>
      <Setter Property='TextAlignment' Value='Center'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='FontSize' Value='13'/><Setter Property='IsUndoEnabled' Value='False'/>
    </Style>
    <Style x:Key='Switch' TargetType='CheckBox'>
      <Setter Property='Foreground' Value='~FG~'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='14'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <DockPanel Background='Transparent' MinHeight='40' Margin='0,8'>
            <Grid DockPanel.Dock='Right' Width='40' Height='22' Margin='16,0,0,0' VerticalAlignment='Center'>
              <Border x:Name='Track' CornerRadius='11' Background='~RAISED~' BorderBrush='~LINE2~' BorderThickness='1'/>
              <Ellipse x:Name='Knob' Width='16' Height='16' Fill='~MUTED~' HorizontalAlignment='Left' Margin='3,0,0,0'>
                <Ellipse.RenderTransform><TranslateTransform/></Ellipse.RenderTransform>
              </Ellipse>
            </Grid>
            <ContentPresenter VerticalAlignment='Center'/>
          </DockPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Trigger.EnterActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='18' Duration='~D150~'><DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction></DoubleAnimation>
              </Storyboard></BeginStoryboard></Trigger.EnterActions>
              <Trigger.ExitActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='Knob' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='0' Duration='~D150~'><DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction></DoubleAnimation>
              </Storyboard></BeginStoryboard></Trigger.ExitActions>
              <Setter TargetName='Track' Property='Background' Value='~ACC~'/><Setter TargetName='Track' Property='BorderBrush' Value='~ACC~'/><Setter TargetName='Knob' Property='Fill' Value='~INK~'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Track' Property='Opacity' Value='0.88'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Track' Property='BorderBrush' Value='~FG~'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Slide' TargetType='Slider'>
      <Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='Slider'>
          <Grid Height='32' Background='Transparent'>
            <Border Height='4' CornerRadius='2' Background='~RAISED~' VerticalAlignment='Center'/>
            <Track x:Name='PART_Track'>
              <Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Style='{StaticResource Fill}'/></Track.DecreaseRepeatButton>
              <Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Style='{StaticResource Clear}'/></Track.IncreaseRepeatButton>
              <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'>
                <Ellipse x:Name='K' Width='16' Height='16' Fill='~FG~' Stroke='~ACC~' StrokeThickness='2'/>
                <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='K' Property='Fill' Value='#FFFFFF'/></Trigger></ControlTemplate.Triggers>
              </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
            </Track>
          </Grid>
          <ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
  </Window.Resources>

  <Grid Margin='32,24,32,24'>
    <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>

    <DockPanel Grid.Row='0' Margin='0,0,0,24'>
      <Border DockPanel.Dock='Right' VerticalAlignment='Center' Background='~OKBG~' BorderBrush='#334ADE80' BorderThickness='1' CornerRadius='14' Padding='12,5'>
        <StackPanel Orientation='Horizontal'>
          <Ellipse Width='8' Height='8' Fill='~OK~' VerticalAlignment='Center' Margin='0,0,8,0'/>
          <TextBlock Text='@badge@' Foreground='~OKFG~' FontSize='12'/>
        </StackPanel>
      </Border>
      <Image x:Name='Logo' FlowDirection='LeftToRight' Width='48' Height='48' Margin='0,0,16,0'/>
      <StackPanel VerticalAlignment='Center'>
        <TextBlock Text='NJ-Sort' FontSize='24' FontWeight='Bold'/>
        <TextBlock Text='@tagline@' Foreground='~MUTED~' FontSize='13' Margin='0,2,0,0'/>
      </StackPanel>
    </DockPanel>

    <Grid Grid.Row='1'>
      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='16'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
      <StackPanel Grid.Column='0'>
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='@h.desktop@'/>
            <TextBlock x:Name='ActiveLine' Style='{StaticResource Sub}' Margin='0,4,0,16'/>
            <UniformGrid Columns='3'>
              <Button x:Name='SaveBtn' Style='{StaticResource Primary}' MinHeight='56' Margin='0,0,4,0' ToolTip='@tip.save@'>
                <StackPanel><TextBlock Text='@btn.save@' HorizontalAlignment='Center'/><TextBlock x:Name='SaveHk' FontSize='11' FontWeight='Normal' Opacity='0.7' HorizontalAlignment='Center' Margin='0,2,0,0'/></StackPanel>
              </Button>
              <Button x:Name='RearrangeBtn' Style='{StaticResource Btn}' MinHeight='56' Margin='4,0,4,0' ToolTip='@tip.rearrange@'>
                <StackPanel><TextBlock Text='@btn.rearrange@' HorizontalAlignment='Center'/><TextBlock x:Name='RearrangeHk' FontSize='11' Foreground='~MUTED~' HorizontalAlignment='Center' Margin='0,2,0,0'/></StackPanel>
              </Button>
              <Button x:Name='UndoBtn' Style='{StaticResource Btn}' MinHeight='56' Margin='4,0,0,0' ToolTip='@tip.undo@'>
                <StackPanel><TextBlock Text='@btn.undo@' HorizontalAlignment='Center'/><TextBlock x:Name='UndoHk' FontSize='11' Foreground='~MUTED~' HorizontalAlignment='Center' Margin='0,2,0,0'/></StackPanel>
              </Button>
            </UniformGrid>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Card}' Margin='0,16,0,0'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='@h.history@'/>
            <TextBlock Style='{StaticResource Sub}' Margin='0,4,0,16' Text='@history.note@'/>
            <Grid>
              <ListBox x:Name='History' Height='224' Background='Transparent' BorderThickness='0' Padding='0' FocusVisualStyle='{x:Null}'
                       ScrollViewer.HorizontalScrollBarVisibility='Disabled'>
                <ListBox.ItemContainerStyle>
                  <Style TargetType='ListBoxItem'>
                    <Setter Property='Cursor' Value='Hand'/><Setter Property='FocusVisualStyle' Value='{x:Null}'/>
                    <Setter Property='Template'><Setter.Value>
                      <ControlTemplate TargetType='ListBoxItem'>
                        <Border x:Name='B' Background='~TILE~' BorderBrush='~LINE~' BorderThickness='1' CornerRadius='8' Padding='12,8' Margin='0,0,4,8' MinHeight='48'>
                          <ContentPresenter VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='~RAISED~'/><Setter TargetName='B' Property='BorderBrush' Value='~LINE2~'/></Trigger>
                          <Trigger Property='IsSelected' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='~ACC~'/><Setter TargetName='B' Property='Background' Value='~RAISED~'/></Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Setter.Value></Setter>
                  </Style>
                </ListBox.ItemContainerStyle>
                <ListBox.ItemTemplate>
                  <DataTemplate>
                    <DockPanel>
                      <Border DockPanel.Dock='Right' Visibility='{Binding ActiveVis}' Background='#26FFB02E' BorderBrush='#55FFB02E' BorderThickness='1' CornerRadius='6' Padding='8,2' VerticalAlignment='Center' Margin='12,0,0,0'>
                        <TextBlock Text='@chip.active@' Foreground='~ACC~' FontSize='11' FontWeight='Bold'/>
                      </Border>
                      <StackPanel>
                        <TextBlock Text='{Binding Title}' Foreground='~FG~' FontSize='14' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/>
                        <TextBlock Text='{Binding Detail}' Foreground='~MUTED~' FontSize='12' Margin='0,2,0,0'/>
                      </StackPanel>
                    </DockPanel>
                  </DataTemplate>
                </ListBox.ItemTemplate>
              </ListBox>
              <TextBlock x:Name='Empty' Style='{StaticResource Sub}' HorizontalAlignment='Center' VerticalAlignment='Center' TextAlignment='Center'
                         Text='@history.empty@'/>
            </Grid>
            <DockPanel x:Name='RenamePanel' Visibility='Collapsed' Margin='0,8,0,0'>
              <Button x:Name='RenameOk' DockPanel.Dock='Right' Style='{StaticResource Primary}' Content='@btn.savename@' Margin='8,0,0,0'/>
              <TextBox x:Name='RenameBox' Style='{StaticResource Field}'/>
            </DockPanel>
            <StackPanel Orientation='Horizontal' Margin='0,8,0,0'>
              <Button x:Name='UseBtn' Style='{StaticResource Btn}' Content='@btn.use@' Margin='0,0,8,0'/>
              <Button x:Name='RenameBtn' Style='{StaticResource Btn}' Content='@btn.rename@' Margin='0,0,8,0'/>
              <Button x:Name='DeleteBtn' Style='{StaticResource Danger}' Content='@btn.delete@' ToolTip='@tip.delete@'/>
            </StackPanel>
          </StackPanel>
        </Border>
      </StackPanel>

      <StackPanel Grid.Column='2'>
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='@h.shortcuts@'/>
            <TextBlock Style='{StaticResource Sub}' Margin='0,4,0,12'
                       Text='@shortcuts.note@'/>
            <StackPanel x:Name='HkRows'/>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Card}' Margin='0,16,0,0' Padding='20,20,20,12'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='@h.behaviour@' Margin='0,0,0,4'/>
            <CheckBox x:Name='CAuto' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='@sw.auto@'/>
                <TextBlock Style='{StaticResource Sub}' Margin='0,2,0,0' Text='@sw.auto_note@'/></StackPanel>
            </CheckBox>
            <Border Style='{StaticResource Rule}'/>
            <CheckBox x:Name='CExtras' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='@sw.extras@'/>
                <TextBlock Style='{StaticResource Sub}' Margin='0,2,0,0' Text='@sw.extras_note@'/></StackPanel>
            </CheckBox>
            <Border Style='{StaticResource Rule}'/>
            <CheckBox x:Name='CAnim' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='@sw.anim@'/>
                <TextBlock Style='{StaticResource Sub}' Margin='0,2,0,0' Text='@sw.anim_note@'/></StackPanel>
            </CheckBox>
            <Border Style='{StaticResource Rule}'/>
            <CheckBox x:Name='CSound' Style='{StaticResource Switch}'><TextBlock Text='@sw.sound@'/></CheckBox>
            <DockPanel Margin='0,-6,0,8'>
              <TextBlock x:Name='VolText' DockPanel.Dock='Right' Style='{StaticResource Sub}' Width='44' TextAlignment='Right' VerticalAlignment='Center'/>
              <TextBlock x:Name='VolLabel' Text='@vol.label@' Style='{StaticResource Sub}' Width='64' VerticalAlignment='Center'/>
              <Slider x:Name='Vol' Style='{StaticResource Slide}' Minimum='0' Maximum='100' IsMoveToPointEnabled='True' VerticalAlignment='Center'/>
            </DockPanel>
            <Border Style='{StaticResource Rule}'/>
            <CheckBox x:Name='CLogon' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='@sw.logon@'/>
                <TextBlock Style='{StaticResource Sub}' Margin='0,2,0,0' Text='@sw.logon_note@'/></StackPanel>
            </CheckBox>
            <Border Style='{StaticResource Rule}'/>
            <StackPanel Margin='0,12,0,8'>
              <TextBlock Text='@lang.label@' FontSize='14'/>
              <TextBlock Style='{StaticResource Sub}' Margin='0,2,0,10' Text='@lang.note@'/>
              <StackPanel x:Name='LangRow' Orientation='Horizontal'/>
            </StackPanel>
          </StackPanel>
        </Border>
      </StackPanel>
    </Grid>

    <DockPanel Grid.Row='2' Margin='0,24,0,0'>
      <Button x:Name='CloseBtn' DockPanel.Dock='Right' Style='{StaticResource Btn}' Content='@btn.close@'/>
      <TextBlock x:Name='Ver' DockPanel.Dock='Right' Foreground='~DIM~' FontSize='12' VerticalAlignment='Center' Margin='0,0,16,0'/>
      <Button x:Name='QuitBtn' Style='{StaticResource Danger}' Content='@btn.quit@' HorizontalAlignment='Left' ToolTip='@tip.quit@'/>
    </DockPanel>
  </Grid>
</Window>";
}
