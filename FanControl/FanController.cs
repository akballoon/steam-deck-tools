﻿using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.CPU;
using Microsoft.VisualBasic.Devices;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FanControl
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    internal partial class FanController : IDisposable
    {
        public enum FanMode
        {
            Default,
            SteamOS,
            Max
        }

        [CategoryAttribute("Fan")]
        public FanMode Mode { get; private set; }

        [CategoryAttribute("Fan")]
        public ushort CurrentRPM { get; private set; }

        [CategoryAttribute("Fan")]
        public ushort DesiredRPM { get; private set; }

        [CategoryAttribute("Board")]
        public String FirmwareVersion { get; private set; } = Vlv0100.GetFirmwareVersion().ToString("X");
        [CategoryAttribute("Board")]
        public String BoardID { get; private set; } = Vlv0100.GetBoardID().ToString("X");
        [CategoryAttribute("Board")]
        public String PDCS { get; private set; } = Vlv0100.GetPDCS().ToString("X");

        private LibreHardwareMonitor.Hardware.Computer libreHardwareComputer = new LibreHardwareMonitor.Hardware.Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsStorageEnabled = true,
            IsBatteryEnabled = true
        };

        public FanController()
        {
            libreHardwareComputer.Open();
        }

        private void visitHardware(IHardware hardware)
        {
            Dictionary<FanSensor, ISensor> matched = new Dictionary<FanSensor, ISensor>();

            foreach (ISensor hwSensor in hardware.Sensors)
            {
                foreach (var sensor in allSensors.Values)
                {
                    if (sensor.Matches(hwSensor))
                        matched[sensor] = hwSensor;
                }
            }

            if (matched.Any())
            {
                hardware.Update();
                foreach (var sensor in matched)
                    sensor.Key.Update(sensor.Value, Mode);
            }

            foreach (IHardware subhardware in hardware.SubHardware)
            {
                visitHardware(subhardware);
            }
        }

        private ushort getDesiredRPM()
        {
            ushort rpm = 0;
            foreach (var sensor in allSensors.Values)
                if (sensor.CalculatedRPM.HasValue)
                    rpm = Math.Max(rpm, sensor.CalculatedRPM.Value);
            return rpm;
        }

        public void Update()
        {
            foreach (var sensor in allSensors.Values)
                sensor.Reset();

            foreach (var hardware in libreHardwareComputer.Hardware)
                visitHardware(hardware);

            allSensors["Batt"].Update("VLV0100", Vlv0100.GetBattTemperature(), Mode);

            Vlv0100.SetFanDesiredRPM(getDesiredRPM());

            CurrentRPM = Vlv0100.GetFanRPM();
            DesiredRPM = Vlv0100.GetFanDesiredRPM();
        }

        public void SetMode(FanMode mode)
        {
            switch (mode)
            {
                case FanMode.Default:
                    Vlv0100.SetFanControl(false);
                    break;

                default:
                    Vlv0100.SetFanControl(true);
                    break;
            }

            this.Mode = mode;
        }

        public bool IsAnyInvalid()
        {
            foreach (var sensor in allSensors.Values)
            {
                if (!sensor.IsValid(Mode))
                    return true;
            }
            return false;
        }

        public void Dispose()
        {
            libreHardwareComputer.Close();
        }
    }
}