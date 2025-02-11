using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HDMI_IN
{
    public class RadioMenuItem : MenuItem
    {
        public string GroupName { get; set; }

        public int Value { get; set; }
        protected override void OnClick()
        {
            if (Parent is ItemsControl ic)
            {
                var rmi = ic.Items.OfType<RadioMenuItem>().FirstOrDefault(i =>
                    i.GroupName == GroupName && i.IsChecked);
                if (null != rmi) rmi.IsChecked = false;

                IsChecked = true;
                Properties.Settings.Default.FrameSizeIndex = Value;
                Properties.Settings.Default.Save();
            }
            base.OnClick();
        }
    }
}
