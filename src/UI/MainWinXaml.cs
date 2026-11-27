// NJ-Sort: the main window's layout and styles, as one XAML string.
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
        Title='NJ-Sort' Width='1010' SizeToContent='Height' ResizeMode='CanMinimize' WindowStartupLocation='CenterScreen'
        Background='#181A3A' Foreground='#ECEEFF' FontFamily='Thmanyah Sans, Segoe UI' FontSize='14' UseLayoutRounding='True'>
  <Window.Resources>
    <SolidColorBrush x:Key='Muted' Color='#AAAED6'/>
    <Style TargetType='ScrollBar'>
      <Setter Property='Width' Value='8'/><Setter Property='MinWidth' Value='8'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Track x:Name='PART_Track' IsDirectionReversed='True' Width='8'>
            <Track.Thumb><Thumb Cursor='Hand'><Thumb.Template><ControlTemplate TargetType='Thumb'>
              <Border x:Name='T' CornerRadius='4' Background='#3A3F80'/>
              <ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='T' Property='Background' Value='#5158A8'/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
          </Track>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Card' TargetType='Border'>
      <Setter Property='Background' Value='#1F2250'/><Setter Property='BorderBrush' Value='#2C3068'/><Setter Property='BorderThickness' Value='1'/>
      <Setter Property='CornerRadius' Value='16'/><Setter Property='Padding' Value='20,18'/>
    </Style>
    <Style x:Key='H' TargetType='TextBlock'><Setter Property='FontSize' Value='17'/><Setter Property='FontWeight' Value='Bold'/></Style>
    <Style x:Key='Sub' TargetType='TextBlock'><Setter Property='Foreground' Value='#AAAED6'/><Setter Property='FontSize' Value='13'/><Setter Property='TextWrapping' Value='Wrap'/><Setter Property='LineHeight' Value='19'/></Style>
    <Style x:Key='Btn' TargetType='Button'>
      <Setter Property='Foreground' Value='#ECEEFF'/><Setter Property='Background' Value='#2D3168'/><Setter Property='Padding' Value='16,9'/>
      <Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='14'/><Setter Property='Focusable' Value='True'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' CornerRadius='10' Padding='{TemplateBinding Padding}' RenderTransformOrigin='0.5,0.5'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Opacity' Value='0.86'/></Trigger>
            <Trigger Property='IsPressed' Value='True'><Setter TargetName='B' Property='RenderTransform'><Setter.Value><ScaleTransform ScaleX='0.97' ScaleY='0.97'/></Setter.Value></Setter></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter TargetName='B' Property='Opacity' Value='0.38'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='Primary' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Background' Value='#FFB02E'/><Setter Property='Foreground' Value='#181A3A'/><Setter Property='FontWeight' Value='Bold'/>
    </Style>
    <Style x:Key='Danger' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Background' Value='#00000000'/><Setter Property='Foreground' Value='#FF8F8F'/>
    </Style>
    <Style x:Key='IconBtn' TargetType='Button' BasedOn='{StaticResource Btn}'>
      <Setter Property='Padding' Value='10,5'/><Setter Property='Background' Value='#00000000'/><Setter Property='Foreground' Value='#AAAED6'/><Setter Property='FontSize' Value='16'/>
    </Style>
    <Style x:Key='Field' TargetType='TextBox'>
      <Setter Property='Foreground' Value='#ECEEFF'/><Setter Property='CaretBrush' Value='#ECEEFF'/><Setter Property='FontSize' Value='14'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='B' Background='#141631' BorderBrush='#353A75' BorderThickness='1.5' CornerRadius='9' Padding='10,7'>
            <ScrollViewer x:Name='PART_ContentHost' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#4A50A0'/></Trigger>
            <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#FFB02E'/><Setter TargetName='B' Property='Background' Value='#1A1C40'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
    <Style x:Key='HkBox' TargetType='TextBox' BasedOn='{StaticResource Field}'>
      <Setter Property='IsReadOnly' Value='True'/><Setter Property='IsReadOnlyCaretVisible' Value='False'/><Setter Property='Cursor' Value='Hand'/>
      <Setter Property='TextAlignment' Value='Center'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='IsUndoEnabled' Value='False'/>
    </Style>
    <Style x:Key='Switch' TargetType='CheckBox'>
      <Setter Property='Foreground' Value='#ECEEFF'/><Setter Property='Cursor' Value='Hand'/><Setter Property='Margin' Value='0,7'/>
      <Setter Property='Template'><Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <DockPanel Background='Transparent' LastChildFill='True'>
            <Border x:Name='Track' DockPanel.Dock='Right' Width='42' Height='24' CornerRadius='12' Background='#353A75' Margin='16,0,0,0' VerticalAlignment='Center'>
              <Ellipse x:Name='Thumb' Width='18' Height='18' Fill='#C9CCEB' HorizontalAlignment='Left' Margin='3,0,0,0'>
                <Ellipse.RenderTransform><TranslateTransform/></Ellipse.RenderTransform>
              </Ellipse>
            </Border>
            <ContentPresenter VerticalAlignment='Center'/>
          </DockPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Trigger.EnterActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='Thumb' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='18' Duration='0:0:0.18'><DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction></DoubleAnimation>
              </Storyboard></BeginStoryboard></Trigger.EnterActions>
              <Trigger.ExitActions><BeginStoryboard><Storyboard>
                <DoubleAnimation Storyboard.TargetName='Thumb' Storyboard.TargetProperty='(UIElement.RenderTransform).(TranslateTransform.X)' To='0' Duration='0:0:0.18'><DoubleAnimation.EasingFunction><CubicEase EasingMode='EaseOut'/></DoubleAnimation.EasingFunction></DoubleAnimation>
              </Storyboard></BeginStoryboard></Trigger.ExitActions>
              <Setter TargetName='Track' Property='Background' Value='#FFB02E'/><Setter TargetName='Thumb' Property='Fill' Value='#181A3A'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Track' Property='Opacity' Value='0.88'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value></Setter>
    </Style>
  </Window.Resources>

  <Grid Margin='28,22,28,22'>
    <Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/><RowDefinition Height='Auto'/></Grid.RowDefinitions>

    <DockPanel Grid.Row='0' Margin='0,0,0,18'>
      <Border DockPanel.Dock='Right' VerticalAlignment='Center' Background='#1B3A33' CornerRadius='13' Padding='12,6'>
        <StackPanel Orientation='Horizontal'>
          <Ellipse Width='8' Height='8' Fill='#4ADE80' VerticalAlignment='Center' Margin='0,0,8,0'/>
          <TextBlock Text='Running in the hidden icons' Foreground='#B6F2C9' FontSize='12.5'/>
        </StackPanel>
      </Border>
      <Image x:Name='Logo' Width='54' Height='54' Margin='0,0,16,0'/>
      <StackPanel VerticalAlignment='Center'>
        <TextBlock Text='NJ-Sort' FontSize='26' FontWeight='Bold'/>
        <TextBlock Text='Keeps your desktop icons in the sort you saved.' Foreground='{StaticResource Muted}'/>
      </StackPanel>
    </DockPanel>

    <Grid Grid.Row='1'>
      <Grid.ColumnDefinitions><ColumnDefinition Width='*'/><ColumnDefinition Width='18'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions>
      <StackPanel Grid.Column='0'>
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Desktop'/>
            <TextBlock x:Name='ActiveLine' Style='{StaticResource Sub}' Margin='0,5,0,14'/>
            <UniformGrid Columns='3'>
              <Button x:Name='SaveBtn' Style='{StaticResource Primary}' Margin='0,0,6,0' ToolTip='Saves where every icon is now as a new sort, and makes it the active one.'>
                <StackPanel><TextBlock Text='Save sort now' HorizontalAlignment='Center'/><TextBlock x:Name='SaveHk' FontSize='12' FontWeight='Normal' Opacity='0.75' HorizontalAlignment='Center'/></StackPanel>
              </Button>
              <Button x:Name='RearrangeBtn' Style='{StaticResource Btn}' Margin='3,0,3,0' ToolTip='Puts the icons back in the active sort. Items that are not in it follow, sorted by name.'>
                <StackPanel><TextBlock Text='Rearrange' HorizontalAlignment='Center'/><TextBlock x:Name='RearrangeHk' FontSize='12' Opacity='0.7' HorizontalAlignment='Center'/></StackPanel>
              </Button>
              <Button x:Name='UndoBtn' Style='{StaticResource Btn}' Margin='6,0,0,0' ToolTip='Moves the icons back to where they were before the last rearrange.'>
                <StackPanel><TextBlock Text='Undo' HorizontalAlignment='Center'/><TextBlock x:Name='UndoHk' FontSize='12' Opacity='0.7' HorizontalAlignment='Center'/></StackPanel>
              </Button>
            </UniformGrid>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Card}' Margin='0,16,0,0'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Sort history'/>
            <TextBlock Style='{StaticResource Sub}' Margin='0,5,0,12' Text='Newest on top. Pick a sort and use it to go back to that point. Double-click works too.'/>
            <Grid>
              <ListBox x:Name='History' Height='214' Background='Transparent' BorderThickness='0' Padding='0'
                       ScrollViewer.HorizontalScrollBarVisibility='Disabled'>
                <ListBox.ItemContainerStyle>
                  <Style TargetType='ListBoxItem'>
                    <Setter Property='Cursor' Value='Hand'/>
                    <Setter Property='Template'><Setter.Value>
                      <ControlTemplate TargetType='ListBoxItem'>
                        <Border x:Name='B' Background='#252859' BorderBrush='#00000000' BorderThickness='1.5' CornerRadius='11' Padding='14,9' Margin='0,0,4,6'>
                          <ContentPresenter/>
                        </Border>
                        <ControlTemplate.Triggers>
                          <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='B' Property='Background' Value='#2C3068'/></Trigger>
                          <Trigger Property='IsSelected' Value='True'><Setter TargetName='B' Property='BorderBrush' Value='#FFB02E'/><Setter TargetName='B' Property='Background' Value='#2C3068'/></Trigger>
                        </ControlTemplate.Triggers>
                      </ControlTemplate>
                    </Setter.Value></Setter>
                  </Style>
                </ListBox.ItemContainerStyle>
                <ListBox.ItemTemplate>
                  <DataTemplate>
                    <DockPanel>
                      <Border DockPanel.Dock='Right' Visibility='{Binding ActiveVis}' Background='#33FFB02E' CornerRadius='9' Padding='9,2' VerticalAlignment='Center' Margin='10,0,0,0'>
                        <TextBlock Text='Active' Foreground='#FFB02E' FontSize='12' FontWeight='Bold'/>
                      </Border>
                      <StackPanel>
                        <TextBlock Text='{Binding Title}' Foreground='#ECEEFF' FontSize='14.5' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/>
                        <TextBlock Text='{Binding Detail}' Foreground='#AAAED6' FontSize='12.5' Margin='0,1,0,0'/>
                      </StackPanel>
                    </DockPanel>
                  </DataTemplate>
                </ListBox.ItemTemplate>
              </ListBox>
              <TextBlock x:Name='Empty' Style='{StaticResource Sub}' HorizontalAlignment='Center' VerticalAlignment='Center' TextAlignment='Center'
                         Text='No sorts yet. Arrange your icons, then press Save sort now.'/>
            </Grid>
            <DockPanel x:Name='RenamePanel' Visibility='Collapsed' Margin='0,8,0,0'>
              <Button x:Name='RenameOk' DockPanel.Dock='Right' Style='{StaticResource Primary}' Content='Save name' Margin='8,0,0,0'/>
              <TextBox x:Name='RenameBox' Style='{StaticResource Field}'/>
            </DockPanel>
            <StackPanel Orientation='Horizontal' Margin='0,10,0,0'>
              <Button x:Name='UseBtn' Style='{StaticResource Btn}' Content='Use this sort' Margin='0,0,8,0'/>
              <Button x:Name='RenameBtn' Style='{StaticResource Btn}' Content='Rename' Margin='0,0,8,0'/>
              <Button x:Name='DeleteBtn' Style='{StaticResource Danger}' Content='Delete' ToolTip='Sends the sort to the Recycle Bin.'/>
            </StackPanel>
          </StackPanel>
        </Border>
      </StackPanel>

      <StackPanel Grid.Column='2'>
        <Border Style='{StaticResource Card}'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Shortcuts'/>
            <TextBlock Style='{StaticResource Sub}' Margin='0,5,0,10'
                       Text='Click a box and press the new keys; Backspace removes it. Shortcuts work while NJ-Sort runs in the hidden icons, and stop when you quit it.'/>
            <StackPanel x:Name='HkRows'/>
          </StackPanel>
        </Border>

        <Border Style='{StaticResource Card}' Margin='0,16,0,0'>
          <StackPanel>
            <TextBlock Style='{StaticResource H}' Text='Behaviour' Margin='0,0,0,6'/>
            <CheckBox x:Name='CAuto' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Put icons back by themselves'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='After Explorer restarts (NJ-Flush, updates), after sign-in and restarts, and after screen changes. It waits until the desktop is steady first.'/></StackPanel>
            </CheckBox>
            <CheckBox x:Name='CExtras' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Keep new items sorted by name'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='Items that are not in the sort go after it on the main screen, folders first, like Windows sort by name.'/></StackPanel>
            </CheckBox>
            <CheckBox x:Name='CAnim' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Animate'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='Icons glide into place. Off when Windows animations are off.'/></StackPanel>
            </CheckBox>
            <CheckBox x:Name='CSound' Style='{StaticResource Switch}'><TextBlock Text='Play sounds'/></CheckBox>
            <DockPanel Margin='0,0,0,6'>
              <TextBlock x:Name='VolText' DockPanel.Dock='Right' Style='{StaticResource Sub}' Width='44' TextAlignment='Right' VerticalAlignment='Center'/>
              <TextBlock Text='Volume' Style='{StaticResource Sub}' Width='70' VerticalAlignment='Center'/>
              <Slider x:Name='Vol' Minimum='0' Maximum='100' IsMoveToPointEnabled='True' VerticalAlignment='Center' Cursor='Hand'/>
            </DockPanel>
            <CheckBox x:Name='CLogon' Style='{StaticResource Switch}'>
              <StackPanel><TextBlock Text='Start with Windows'/>
                <TextBlock Style='{StaticResource Sub}' FontSize='12.5' Text='Starts in the hidden icons when you sign in.'/></StackPanel>
            </CheckBox>
          </StackPanel>
        </Border>
      </StackPanel>
    </Grid>

    <DockPanel Grid.Row='2' Margin='0,18,0,0'>
      <Button x:Name='CloseBtn' DockPanel.Dock='Right' Style='{StaticResource Btn}' Content='Close (keeps running)'/>
      <TextBlock x:Name='Ver' DockPanel.Dock='Right' Foreground='#6E73A8' FontSize='12.5' VerticalAlignment='Center' Margin='0,0,16,0'/>
      <Button x:Name='QuitBtn' Style='{StaticResource Danger}' Content='Quit NJ-Sort' HorizontalAlignment='Left' ToolTip='Stops NJ-Sort. Its shortcuts stop working until you open it again.'/>
    </DockPanel>
  </Grid>
</Window>";
}
