﻿using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using WinForm = System.Windows.Forms;
using System.Text.RegularExpressions;
using Path = System.IO.Path;

namespace l4d2_mutation_creator
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public void HasFoundGame(string GameDir, bool rewrite)
        {
            GameOption.RootPath = GameDir;
            tbxGameDir.Text = GameDir;
            tbxGameDir.IsEnabled = false;
            lblHasFindGame.Content = "已找到游戏";
            lblHasFindGame.Foreground = Brushes.Green;
            btnGenVPK.IsEnabled = true;
            btnExport.IsEnabled = true;

            if (rewrite)
            {
                using (StreamWriter sw = new StreamWriter("path.dat"))
                {
                    sw.WriteLine(GameDir);
                }
            }
        }

        private void IntegerOnly(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex(@"[^0-9]+");
            e.Handled = re.IsMatch(e.Text);
        }
        
        private void RealOnly(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex(@"^[.][0-9]+$|^[0-9]*[.]{0,1}[0-9]*$");
            e.Handled = !re.IsMatch(e.Text);
        }

        private void AlphabetOnly(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex(@"[^a-z]+");
            e.Handled = re.IsMatch(e.Text);
        }

        private void HypeLink_OnClick(object sender, RoutedEventArgs e)
        {
            Hyperlink hpl = (Hyperlink)sender;
            System.Diagnostics.Process.Start(hpl.TargetName);
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            WinForm.FolderBrowserDialog dialog = new WinForm.FolderBrowserDialog();
            dialog.Description = "请选择Left 4 Dead 2的根目录";
            if (WinForm.DialogResult.OK == dialog.ShowDialog())
            {
                string exepath = Path.Combine(dialog.SelectedPath, "left4dead2.exe");
                if (File.Exists(exepath))
                {
                    HasFoundGame(dialog.SelectedPath, true);
                }
            }
        }

        private void RdbPlayerNumber_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded)
            {
                RadioButton rdb = (RadioButton)sender;
                GameOption.PlayerNumber = Convert.ToInt32(rdb.Tag);
            }
        }

        private void GenVPK(string targetDir)
        {
            // ID 不能为空或 VSLib
            if ("" == tbxMutID.Text || "vslib" == tbxMutID.Text)
            {
                MessageBox.Show("变异ID名不能为这三种之一：为空、vslib、gamemodes，请重新修改", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            // 处理cvar
            //if (true == chkReviveFull.IsChecked)
            //    GameOption.Convars["survivor_revive_health"] = "100";

            try
            {
                // addoninfo.txt
                // ignore description
                App.WriteGameInfo(tbxMutName.Text, tbxAuthor.Text, tbxSummary.Text);

                // modes/<mutID>.txt
                App.DelectOldFiles();
                App.WriteGameMode(tbxMutName.Text, tbxMutID.Text,
                    tbxSummary.Text, tbxAuthor.Text);

                // vscripts/<mutID>.nut
                App.WriteGameScript(tbxMutID.Text, GenDirector(), GenHookFuncs());

                App.CallExeByProcess("bin/vpk.exe", "template");

                File.Copy("template.vpk", Path.Combine(targetDir, tbxMutID.Text + ".vpk"), true);
                MessageBox.Show("VPK生成成功！路径："+targetDir, "", MessageBoxButton.OK);
            }
            catch (Exception)
            {
                MessageBox.Show("导出文件失败！可能原因及解决方案：\r\n" +
                    "1. 同名ID已在游戏中打开，请更换ID再生成，或在附加组件中取消上一次的生成结果再重试\r\n" +
                    "2. 程序对目标目录没有写入权限", "文件错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GenDirector()
        {
            string DirectorOptions = "DirectorOptions <- {\r\n";

            // 处理 “杂项修改”
            if (true == chkHeadShot.IsChecked)
                DirectorOptions += "cm_HeadshotOnly = 1\r\n";
            if (true == chkTempHealth.IsChecked)
            {
                string decay;
                if (string.IsNullOrEmpty(tbxDecayRate.Text))
                    decay = "0.27";
                else
                    decay = tbxDecayRate.Text;

                DirectorOptions += "cm_TempHealthOnly = 1\r\n";
                DirectorOptions += ("TempHealthDecayRate = 0.001\r\n" +
                                   "function RecalculateHealthDecay() {\r\n" +
                                   "if (Director.HasAnySurvivorLeftSafeArea())\r\n" +
                                   "TempHealthDecayRate = " +
                                   decay + "}\r\n");
            }
            if (true == rdbOnePlayer.IsChecked)
            {
                DirectorOptions += "cm_NoSurvivorBots = 1\r\n";
            }

            // 处理感染者是否可刷新
            CheckBox[] chkSIAvability = {
                chkBoomer, chkSpitter, chkHunter, chkJockey,
                chkSmoker, chkCharger, chkTank, chkCommon
            };
            foreach (CheckBox chk in chkSIAvability)
            {
                if (false == chk.IsChecked)
                {
                    DirectorOptions += (chk.Content.ToString() + "Limit = 0\r\n");
                }
            }

            // 处理刷新上限及频率
            if (true == chkNoBoss.IsChecked)
            {
                DirectorOptions += "cm_ProhibitBosses = true\r\n";
            }
            else
            {
                DirectorOptions += "cm_ProhibitBosses = false\r\n";
            }

            TextBox[] tbxLimit = {
                tbxLimitBoomer, tbxLimitSpitter, tbxLimitHunter, tbxLimitJockey,
                tbxLimitSmoker, tbxLimitCharger
            };
            foreach (TextBox tbx in tbxLimit)
            {
                if (true == tbx.IsEnabled)
                {
                    DirectorOptions += (tbx.Tag.ToString()+"Limit = "+tbx.Text+"\r\n");
                }
            }
            DirectorOptions += ("cm_TankLimit = " + tbxLimitTank.Text + "\r\n");

            if (true == chkInfTotalSpecial.IsChecked)
            {
                DirectorOptions += "TotalSpecials = 999\r\n";
            }
            DirectorOptions += "cm_MaxSpecials = " 
                               + Convert.ToInt32(tbxMaxSpecials.Text) + "\r\n";
            DirectorOptions += string.Format("cm_SpecialRespawnInterval = {0}\r\n",
                               ((ComboBoxItem)cmbInterval.SelectedItem).Tag.ToString());
            int SIDelay = Convert.ToInt32(tbxInitSpecialInterval.Text);
            DirectorOptions += "SpecialInitialSpawnDelayMin = " + SIDelay.ToString() + "\r\n";
            DirectorOptions += "SpecialInitialSpawnDelayMax = " + (SIDelay+3).ToString() + "\r\n";
            DirectorOptions += "ShouldConstrainLargeVolumeSpawn = false\r\n";
            DirectorOptions += "PreferredMobDirection = SPAWN_ANYWHERE\r\n";
            DirectorOptions += "PreferredSpecialDirection = SPAWN_SPECIALS_ANYWHERE\r\n";

            // 处理倒地状态
            if (true == chkIncapDying.IsChecked)
            {
                DirectorOptions += "cm_AutoReviveFromSpecialIncap = 1\r\n";
                DirectorOptions += "SurvivorMaxIncapacitatedCount = 1\r\n";
            }
            else if (true == chkIncapDeath.IsChecked)
            {
                DirectorOptions += "SurvivorMaxIncapacitatedCount = 0\r\n";
            }
            else if (true == chkIncapEscape.IsChecked)
            {
                DirectorOptions += "cm_AutoReviveFromSpecialIncap = 1\r\n";
                DirectorOptions += "SurvivorMaxIncapacitatedCount = 999\r\n";
            }

            // 处理禁止刷新的资源
            string RemoveItems = "weaponsToRemove = {\r\n";
            foreach (string item in GameOption.WeaponToRemove)
            {
                RemoveItems += (item + "\r\n");
            }
            RemoveItems += "}\r\n";

            DirectorOptions += RemoveItems;
            DirectorOptions += "function AllowWeaponSpawn(classname){\r\n" +
                               "if (classname in weaponsToRemove) return false;\r\n" +
                               "return true;}\r\n";

            // 处理止痛药转换和隐藏武器
            string WeaponsToConvert = "weaponsToConvert={\r\n";
            if (true == chkPillConvertion.IsChecked)
                WeaponsToConvert += "weapon_first_aid_kit=\"weapon_pain_pills_spawn\"\r\n";
            if (true == chkAWP.IsChecked)
                WeaponsToConvert += "weapon_sniper_military=\"weapon_sniper_awp_spawn\"\r\n";
            if (true == chkMP5.IsChecked)
                WeaponsToConvert += "weapon_smg=\"weapon_smg_spawn\"\r\n";
            if (true == chkScout.IsChecked)
                WeaponsToConvert += "weapon_hunting_rifle=\"weapon_sniper_scout_spawn\"\r\n";
            if (true == chkSG552.IsChecked)
                WeaponsToConvert += "weapon_rifle=\"weapon_rifle_sg552_spawn\"\r\n";

            WeaponsToConvert += "}\r\n";

            DirectorOptions += WeaponsToConvert;
            DirectorOptions += "function ConvertWeaponSpawn(classname){\r\n" +
                               "if (classname in weaponsToConvert)\r\n" +
                               "return weaponsToConvert[classname];\r\n" +
                               "return 0;}\r\n";


            // 处理游戏节奏
            if (true == chkLockTempo.IsChecked)
                DirectorOptions += "LockTempo = 1\r\n";

            string Tempo = "";
            foreach (var item in GameOption.Tempo)
            {
                Tempo += (item.Key + " = " + item.Value + "\r\n");
            }
            DirectorOptions += Tempo;

            // 处理初始武器
            string DefaultItems = "DefaultItems=[\r\n";
            foreach (string item in GameOption.InitCarries)
            {
                DefaultItems += ('"' + item + "\"\r\n");
            }
            DefaultItems += "]\r\n";
            DirectorOptions += DefaultItems;
            DirectorOptions += "function GetDefaultItem(idx){\r\n" +
                               "if (idx<DefaultItems.len()) return DefaultItems[idx];\r\n" +
                               "return 0;}\r\n";

            DirectorOptions += "}\r\n";
            return DirectorOptions;
        }

        private string GenHookFuncs()
        {
            string HookFuncs = string.Format("function Notifications::" +
                "OnTankSpawned::ResetTankHealth(tank, params){{\r\n" +
                "tank.SetMaxHealth({0});\r\n" +
                "tank.SetHealth({1});}}\r\n", tbxTank.Text, tbxTank.Text);
            // 重设体力
            TextBox[] tbxHealth = {
                tbxBoomer, tbxSpitter, tbxHunter, tbxJockey,
                tbxSmoker, tbxCharger
            };
            foreach (TextBox tbx in tbxHealth)
            {
                if (true == tbx.IsEnabled)
                {
                    string SIName = tbx.Tag.ToString();
                    string Health = tbx.Text;
                    HookFuncs += string.Format("function Notifications::OnSpawn" +
                        "::Reset{0}Health(player, params){{\r\n" +
                        "if(player.GetPlayerType()==Z_{1}){{\r\n" +
                        "player.SetMaxHealth({2});\r\n" +
                        "player.SetHealth({3});}}}}\r\n",
                        SIName, SIName.ToUpper(), Health, Health);
                }
            }

            // OnReviveSuccess
            if (false == string.IsNullOrWhiteSpace(tbxIncapHealth.Text))
            {
                int health = Convert.ToInt32(tbxIncapHealth.Text);
                if (health > 30)
                {
                    HookFuncs += "function Notifications::OnReviveSuccess" +
                                 "::ResetHealth(revivee, reviver, params){" +
                                 "revivee.IncreaseHealth(" +
                                 (health-30).ToString() + ");}\r\n";
                }
                else if (health < 30)
                {
                    HookFuncs += "function Notifications::OnReviveSuccess" +
                                 "::ResetHealth(revivee, reviver, params){" +
                                 "revivee.DecreaseHealth(" +
                                 (30 - health).ToString() + ");}\r\n";
                }
            }

            // OnSurvivorRescued
            if (true == chkReviveFull.IsChecked)
            {
                HookFuncs += "function Notifications::OnSurvivorRescued" +
                             "::SetFullHealth(rescuer, victim, params){" +
                             "victim.SetHealth(100);}\r\n";
            }

            // 无限备弹
            if (true == chkInfAmmo.IsChecked)
            {
                HookFuncs += "function Notifications::OnWeaponReload" +
                             "::ReplenishAmmo(player, ismanual, params){" +
                             "player.GiveAmmo(100);}\r\n";
            }

            // Update
            if (true == chkTempHealth.IsChecked)
            {
                HookFuncs += "function Update(){DirectorOptions.RecalculateHealthDecay();}\r\n";
            }
            return HookFuncs;
        }

        private void BtnGenVPK_Click(object sender, RoutedEventArgs e)
        {
            string AddonPath = Path.Combine(GameOption.RootPath, "left4dead2/addons");
            GenVPK(AddonPath);
        }

        private void ChkCoop_Checked(object sender, RoutedEventArgs e)
        {
            GameOption.BaseGame = "coop";
        }

        private void ChkRealism_Checked(object sender, RoutedEventArgs e)
        {
            GameOption.BaseGame = "realism";
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            GenVPK(dir);
        }

        private void TbxHealth_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                TextBox tbx = (TextBox)sender;
                if (false == string.IsNullOrWhiteSpace(tbx.Text))
                {
                    GameOption.SI[tbx.Tag.ToString()].SetHealth(Convert.ToInt32(tbx.Text));
                }
            }
        }

        private void TbxLimit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                TextBox tbx = (TextBox)sender;
                if (false == string.IsNullOrWhiteSpace(tbx.Text))
                {
                    GameOption.SI[tbx.Tag.ToString()].SetLimit(Convert.ToInt32(tbx.Text));
                }
            }
        }

        private void TbxTempo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                TextBox tbx = (TextBox)sender;
                if (false == string.IsNullOrWhiteSpace(tbx.Text))
                {
                    GameOption.Tempo[tbx.Tag.ToString()] = Convert.ToInt32(tbx.Text);
                }
            }
        }

        private void TbxCvars_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.IsLoaded)
            {
                TextBox tbx = (TextBox)sender;
                if (false == string.IsNullOrWhiteSpace(tbx.Text))
                {
                    GameOption.Convars[tbx.Tag.ToString()] = tbx.Text;
                }
            }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TextBox[] tbxHealth = {
                tbxBoomer, tbxSpitter, tbxHunter, tbxJockey,
                tbxSmoker, tbxCharger, tbxTank
            };
            TextBox[] tbxLimit = {
                tbxLimitBoomer, tbxLimitSpitter, tbxLimitHunter, tbxLimitJockey,
                tbxLimitSmoker, tbxLimitCharger, tbxLimitTank
            };

            for (int i = 0; i < 7; i++)
            {
                tbxHealth[i].Text = GameOption.DefaultSI[tbxHealth[i].Tag.ToString()]
                    .GetHealth().ToString();
                tbxLimit[i].Text = GameOption.DefaultSI[tbxLimit[i].Tag.ToString()]
                    .GetLimit().ToString();
            }
            tbxLimitCommon.Text = GameOption.DefaultSI["Common"].GetLimit().ToString();
        }

        private void ChkWeapon_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded)
            {
                CheckBox chk = (CheckBox)sender;
                string weapon = chk.Tag.ToString();
                GameOption.WeaponToRemove.Remove(weapon + " = 0");
            }
        }

        private void ChkWeapon_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded)
            {
                CheckBox chk = (CheckBox)sender;
                string weapon = chk.Tag.ToString();
                GameOption.WeaponToRemove.Add(weapon + " = 0");
            }
        }

        private void ChkItem_Checked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded)
            {
                CheckBox chk = (CheckBox)sender;
                string item = chk.Tag.ToString();
                GameOption.WeaponToRemove.Remove(item + " = 0");
            }
        }

        private void ChkItem_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded)
            {
                CheckBox chk = (CheckBox)sender;
                string item = chk.Tag.ToString();
                GameOption.WeaponToRemove.Add(item + " = 0");
            }
        }

        private void BtnTempoHelp_Click(object sender, RoutedEventArgs e)
        {
            App.wndTempoHelp.Show();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            App.wndTempoHelp.Close();
        }

        private void CmbInitCarries_Selected(object sender, RoutedEventArgs e)
        {
            if (this.IsLoaded)
            {
                ComboBoxItem item = (ComboBoxItem)sender;
                GameOption.InitCarries[Convert.ToInt32(item.Uid)] = item.Tag.ToString();
            }
        }

    }

}
