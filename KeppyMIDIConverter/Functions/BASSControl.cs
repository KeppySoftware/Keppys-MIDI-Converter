﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Enc;
using Un4seen.Bass.AddOn.Midi;
using Un4seen.Bass.AddOn.Vst;
using Un4seen.BassWasapi;

namespace KeppyMIDIConverter
{
    class BASSControl
    {
        public static void ReleaseResources(bool stillrendering)
        {
            MainWindow.KMCGlobals.DoNotCountNotes = false;
            BassWasapi.BASS_WASAPI_Stop(true);
            BassWasapi.BASS_WASAPI_Free();
            Bass.BASS_StreamFree(MainWindow.KMCGlobals._recHandle);
            Bass.BASS_Free();
            MainWindow.KMCStatus.IsKMCBusy = stillrendering;
            MainWindow.KMCStatus.IsKMCNowExporting = false;
            MainWindow.KMCGlobals.eventc = 0;
            MainWindow.KMCGlobals.events = null;
        }

        public static void BASSCloseStream(string message, string title, int type)
        {
            MainWindow.KMCGlobals.DoNotCountNotes = false;
            BassWasapi.BASS_WASAPI_Stop(true);
            BassWasapi.BASS_WASAPI_Free();
            Bass.BASS_StreamFree(MainWindow.KMCGlobals._recHandle);
            Bass.BASS_Free();
            MainWindow.KMCGlobals._LoudMaxHanParams = null;
            MainWindow.VSTs.VSTParams = new float?[8][] { null, null, null, null, null, null, null, null };
            MainWindow.KMCGlobals.CurrentStatusTextString = message;
            MainWindow.KMCGlobals.ActiveVoicesInt = 0;
            MainWindow.KMCGlobals.NewWindowName = null;
            MainWindow.KMCStatus.IsKMCBusy = false;
            MainWindow.KMCStatus.IsKMCNowExporting = false;
            MainWindow.KMCStatus.RenderingMode = false;
            MainWindow.KMCGlobals.DoNotCountNotes = false;
            MainWindow.KMCGlobals.eventc = 0;
            MainWindow.KMCGlobals.events = null;
            RTF.CPUUsage = 0.0f;
            RTF.ActiveVoices = 0.0f;
            if (type == 0)
            {
                if (Properties.Settings.Default.ShowBalloon)
                {
                    MainWindow.Delegate.Invoke((MethodInvoker)delegate {
                        NotifyArea.ShowStatusTray(title, message, ToolTipIcon.Info);
                    });
                }
                else
                {
                    MainWindow.Delegate.Invoke((MethodInvoker)delegate {
                        MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    });
                }
            }
            else
            {
                if (Properties.Settings.Default.ShowBalloon)
                {
                    MainWindow.Delegate.Invoke((MethodInvoker)delegate {
                        NotifyArea.ShowStatusTray(title, message, ToolTipIcon.Info);
                    });
                }
            }
            MainWindow.KMCGlobals.CancellationPendingValue = 0;
            MainWindow.KMCGlobals.CurrentStatusTextString = null;
            BasicFunctions.PlayConversionStop();
        }

        public static void BASSCloseStreamCrash(Exception ex)
        {
            Bass.BASS_StreamFree(MainWindow.KMCGlobals._recHandle);
            Bass.BASS_Free();
            MainWindow.KMCGlobals._LoudMaxHanParams = null;
            MainWindow.VSTs.VSTParams = new float?[8][] { null, null, null, null, null, null, null, null };
            MainWindow.KMCGlobals.NewWindowName = null;
            MainWindow.KMCStatus.IsKMCBusy = false;
            MainWindow.KMCStatus.IsKMCNowExporting = false;
            MainWindow.KMCStatus.RenderingMode = false;
            MainWindow.KMCGlobals.DoNotCountNotes = false;
            MainWindow.KMCGlobals.eventc = 0;
            MainWindow.KMCGlobals.events = null;
            MainWindow.Delegate.Invoke((MethodInvoker)delegate {
                ErrorHandler errordialog = new ErrorHandler(Languages.Parse("Error"), ex.ToString(), 0, 1);
                errordialog.ShowDialog();
            });
        }

        /* 
       
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void BASSCheckError()
        {
            BASSError Error = Bass.BASS_ErrorGetCode();
            if (Error != 0)
            {
                MethodBase SFError = new StackFrame(1, true).GetMethod();
                throw new Exception(String.Format("Calling method: {0} ({1})\nBASS Error: {2}", SFError.Name, SFError.ToString(), Error.ToString()));
            }
        }
             
        */

        public static void BASSInitSystem(Boolean PreviewMode)
        {
            try
            {
                Bass.BASS_StreamFree(MainWindow.KMCGlobals._recHandle);
                Bass.BASS_Free();
                Bass.BASS_Init(0, Properties.Settings.Default.AudioFreq, BASSInit.BASS_DEVICE_NOSPEAKER, IntPtr.Zero);
                if (!PreviewMode)
                {
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_MIDI_VOICES, 100000);
                }
                else
                {
                    BassWasapi.BASS_WASAPI_Init(-1, 0, 2, BASSWASAPIInit.BASS_WASAPI_BUFFER, 0, 0, null, IntPtr.Zero);
                    BASS_WASAPI_DEVICEINFO info = new BASS_WASAPI_DEVICEINFO();
                    BassWasapi.BASS_WASAPI_GetDeviceInfo(BassWasapi.BASS_WASAPI_GetDevice(), info);
                    MainWindow.KMCGlobals.RealTimeFreq = info.mixfreq;
                    BassWasapi.BASS_WASAPI_Free();
                    Bass.BASS_Free();
                    Bass.BASS_Init(0, info.mixfreq, BASSInit.BASS_DEVICE_NOSPEAKER, IntPtr.Zero);
                    Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_MIDI_VOICES, 2000);
                }
            }
            catch (Exception ex)
            {
                BASSCloseStreamCrash(ex);
            }
        }

        public static void BASSVSTShowDialog(bool vsti, int towhichstream, int whichvst, BASS_VST_INFO vstInfo)
        {
            try
            {
                if (vstInfo.hasEditor && !MainWindow.KMCGlobals.VSTSkipSettings)
                {
                    Form f = new Form();
                    f.Width = vstInfo.editorWidth + 4;
                    f.Height = vstInfo.editorHeight + 34;
                    f.ClientSize = new Size(vstInfo.editorWidth, vstInfo.editorHeight);
                    f.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                    f.StartPosition = FormStartPosition.CenterParent;
                    f.Text = String.Format("{0} {1}", Languages.Parse("DSPSettings"), vstInfo.effectName);
                    BassVst.BASS_VST_EmbedEditor(whichvst, f.Handle);
                    f.ShowDialog();
                    BassVst.BASS_VST_EmbedEditor(whichvst, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("==== Start of Keppy's MIDI Converter Error ====");
                sb.AppendLine(ex.ToString());
                sb.AppendLine("====  End of Keppy's MIDI Converter Error  ====");
                Thread thread = new Thread(() => Clipboard.SetText(sb.ToString()));
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                ErrorHandler errordialog = new ErrorHandler(Languages.Parse("VSTInvalidCallTitle"), Languages.Parse("VSTInvalidCallError"), 0, 0);
                errordialog.ShowDialog();
                if (!vsti) BassVst.BASS_VST_ChannelRemoveDSP(towhichstream, whichvst);
            }
        }

        public static void LoudMaxInit(int towhichstream)
        {
            if (Properties.Settings.Default.LoudMaxEnabled == true && MainWindow.KMCStatus.RenderingMode == true)
            {
                BASS_VST_INFO LMInfo = new BASS_VST_INFO();

                MainWindow.KMCGlobals._LoudMaxHan = BassVst.BASS_VST_ChannelSetDSP(
                    towhichstream,
                    String.Format("{0}\\LoudMax.dll", Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)),
                    BASSVSTDsp.BASS_VST_KEEP_CHANS, 9);
                int vstParams = BassVst.BASS_VST_GetParamCount(MainWindow.KMCGlobals._LoudMaxHan);

                if (vstParams > 0 && (BassVst.BASS_VST_GetInfo(MainWindow.KMCGlobals._LoudMaxHan, LMInfo) && LMInfo.hasEditor))
                {
                    if (MainWindow.KMCGlobals._LoudMaxHanParams == null)
                    {
                        BASSVSTShowDialog(false, towhichstream, MainWindow.KMCGlobals._LoudMaxHan, LMInfo);

                        // Store the values
                        MainWindow.KMCGlobals._LoudMaxHanParams = new float?[vstParams];
                        for (int e = 0; e < vstParams; e++)
                            MainWindow.KMCGlobals._LoudMaxHanParams.SetValue(BassVst.BASS_VST_GetParam(MainWindow.KMCGlobals._LoudMaxHan, e), e);
                    }
                    else
                    {
                        // Restore the values
                        for (int e = 0; e < vstParams; e++)
                            BassVst.BASS_VST_SetParam(MainWindow.KMCGlobals._LoudMaxHan, e, (float)MainWindow.KMCGlobals._LoudMaxHanParams.GetValue(e));
                    }
                }
                LMInfo = null;
            }
        }

        public static void BASSVSTInit(int towhichstream)
        {
            try
            {
                if (MainWindow.KMCStatus.VSTMode == true)
                {
                    LoudMaxInit(towhichstream);

                    for (int i = 0; i <= 7; i++)
                    {
                        if (!MainWindow.VSTs.VSTInfo[i].isInstrument) // VSTi check
                        {
                            MainWindow.VSTs._VSTHandles[i] = BassVst.BASS_VST_ChannelSetDSP(towhichstream, MainWindow.VSTs.VSTDLLs[i], BASSVSTDsp.BASS_VST_DEFAULT, i);
                            int vstParams = BassVst.BASS_VST_GetParamCount(MainWindow.VSTs._VSTHandles[i]);
                            BASS_VST_PARAM_INFO paramInfo = new BASS_VST_PARAM_INFO();

                            if (vstParams > 0 && (BassVst.BASS_VST_GetInfo(MainWindow.VSTs._VSTHandles[i], MainWindow.VSTs.VSTInfo[i]) && MainWindow.VSTs.VSTInfo[i].hasEditor))
                            {
                                if (MainWindow.VSTs.VSTParams[i] == null)
                                {
                                    BASSVSTShowDialog(false, towhichstream, MainWindow.VSTs._VSTHandles[i], MainWindow.VSTs.VSTInfo[i]);

                                    // Store the values
                                    MainWindow.VSTs.VSTParams[i] = new float?[vstParams];
                                    for (int e = 0; e < vstParams; e++)
                                        MainWindow.VSTs.VSTParams[i].SetValue(BassVst.BASS_VST_GetParam(MainWindow.VSTs._VSTHandles[i], e), e);
                                }
                                else
                                {
                                    // Restore the values
                                    for (int e = 0; e < vstParams; e++)
                                        BassVst.BASS_VST_SetParam(MainWindow.VSTs._VSTHandles[i], e, (float)MainWindow.VSTs.VSTParams[i].GetValue(e));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BASSCloseStreamCrash(ex);
            }
        }

        public static void BASSInitVSTiIfNeeded(Boolean PreviewMode)
        {
            try
            {
                if (MainWindow.VSTs.VSTInfo[0].isInstrument)
                {
                    MainWindow.VSTs._VSTHandles[0] = BassVst.BASS_VST_ChannelCreate((MainWindow.KMCStatus.RenderingMode ? Properties.Settings.Default.AudioFreq : MainWindow.KMCGlobals.RealTimeFreq), 2, MainWindow.VSTs.VSTDLLs[0], BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT | (PreviewMode ? 0 : BASSFlag.BASS_MIDI_SINCINTER));

                    if (Bass.BASS_ErrorGetCode() != BASSError.BASS_OK) throw new Exception(String.Format("{0} is not a valid VST instrument!", MainWindow.VSTs.VSTDLLs[0]));

                    if (MainWindow.VSTs.VSTInfo[0].hasEditor)
                    {
                        MainWindow.KMCGlobals._plm = new Un4seen.Bass.Misc.DSP_PeakLevelMeter(MainWindow.VSTs._VSTHandles[0], 0);
                        MainWindow.KMCGlobals._plm.CalcRMS = true;
                        BASSVSTShowDialog(true, MainWindow.KMCGlobals._recHandle, MainWindow.VSTs._VSTHandles[0], MainWindow.VSTs.VSTInfo[0]);
                    }
                    MainWindow.KMCGlobals._myVSTSync = new SYNCPROC(VSTProc);
                    int sync = Bass.BASS_ChannelSetSync(MainWindow.KMCGlobals._recHandle, BASSSync.BASS_SYNC_MIDI_EVENT, 0, MainWindow.KMCGlobals._myVSTSync, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                BASSCloseStreamCrash(ex);
            }
        }

        public static void TempoSync(int handle, int channel, int data, IntPtr user)
        {
            SetTempo(true, true);
        }

        public static void NoteSyncProc(int handle, int channel, int data, IntPtr user)
        {
            if ((data & 0xFF00) != 0) MainWindow.KMCStatus.PlayedNotes += 1;
        }

        public static int MyWasapiProc(IntPtr buffer, Int32 length, IntPtr user)
        {
            int data = Bass.BASS_ChannelGetData(
                MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle,
                buffer, length);
            if (data < 0) MainWindow.KMCGlobals.CancellationPendingValue = 2;
            return data;
        }

        public static void VSTProc(int handle, int channel, int data, IntPtr user)
        {
            int evento = (data >> 0x18);
            int midichan = (data >> 0x10) & 0xFF;
            int param = (data & 0xFFFF);
            BassVst.BASS_VST_ProcessEvent(MainWindow.VSTs._VSTHandles[0], midichan, (BASSMIDIEvent)evento, param);
        }

        public class MidiFilterProcAnalytic
        {
            public string TrackName { get; set; }
            public bool ToIgnore { get; set; }
        }

        public static Boolean[] TracksList;
        public static bool MidiFilterProc(int handle, int track, BASS_MIDI_EVENT midievent, bool seeking, IntPtr user)
        {
            try
            {
                if (midievent.eventtype == BASSMIDIEvent.MIDI_EVENT_NOTE)
                {
                    if (TracksList[track]) return false;

                    int vel = (midievent.param >> 0x08) & 0xFF;
                    int note = midievent.param & 0xFF;

                    // First check
                    if ((Properties.Settings.Default.IgnoreNotes1 && vel > 0) && (vel >= Properties.Settings.Default.IgnoreNotesLow && vel <= Properties.Settings.Default.IgnoreNotesHigh))
                        return false;

                    // Second
                    if (Properties.Settings.Default.Limit88 && (midievent.chan != 9 && !(note >= 21 && note <= 108)))
                        return false;
                }

                return true; // process the event
            }
            catch (Exception ex)
            {
                BASSCloseStreamCrash(ex);
                return false;
            }
        }

        // SF system
        static BASS_MIDI_FONTEX[] Presets;
        private static bool LoadSpecificSoundFont(string path, ref int sfnum)
        {
            if (File.Exists(path))
            {
                Presets[sfnum].font = BassMidi.BASS_MIDI_FontInit(path);
                Presets[sfnum].dpreset = -1;
                Presets[sfnum].dbank = 0;
                Presets[sfnum].spreset = -1;
                Presets[sfnum].sbank = -1;
                Presets[sfnum].dbanklsb = 0;
                if (!Properties.Settings.Default.PreloadSamplesNotSF) BassMidi.BASS_MIDI_FontLoad(Presets[sfnum].font, -1, -1);
                sfnum++;
                return true;
            }
            else return false;

            if (File.Exists(path))
            {
                Presets[sfnum].font = BassMidi.BASS_MIDI_FontInit(path);
                Presets[sfnum].dpreset = -1;
                Presets[sfnum].dbank = 0;
                Presets[sfnum].spreset = -1;
                Presets[sfnum].sbank = -1;
                Presets[sfnum].dbanklsb = 0;
                if (!Properties.Settings.Default.PreloadSamplesNotSF) BassMidi.BASS_MIDI_FontLoad(Presets[sfnum].font, -1, -1);
                sfnum++;
                return true;
            }
            else return false;
        }

        public static void BASSLoadSoundFonts(String str)
        {           
            if (MainWindow.VSTs.VSTInfo[0].isInstrument == false)
            {
                int sfnum = 0;

                if (MainWindow.SoundFontChain.SoundFonts.Length == 0)
                {
                    DirectoryInfo PathToGenericSF = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
                    String FullPath = String.Format("{0}\\GMGeneric.sf2", PathToGenericSF.Parent.FullName);

                    Presets = new BASS_MIDI_FONTEX[1];
                    if (LoadSpecificSoundFont(FullPath, ref sfnum) != true) BASSCloseStreamCrash(new InvalidSoundFont("No SoundFonts available!"));
                    BassMidi.BASS_MIDI_StreamSetFonts(MainWindow.KMCGlobals._recHandle, Presets, sfnum);
                    if (Properties.Settings.Default.PreloadSamplesNotSF) BassMidi.BASS_MIDI_StreamLoadSamples(MainWindow.KMCGlobals._recHandle);
                }
                else
                {
                    if (Properties.Settings.Default.PreloadSFOfMIDI)
                    {
                        String SFToLoad = String.Format("{0}\\{1}.sf2", Path.GetDirectoryName(str), Path.GetFileNameWithoutExtension(str));
                        Presets = new BASS_MIDI_FONTEX[1];
                        if (LoadSpecificSoundFont(SFToLoad, ref sfnum))
                        {
                            BassMidi.BASS_MIDI_StreamSetFonts(MainWindow.KMCGlobals._recHandle, Presets, sfnum);
                            if (Properties.Settings.Default.PreloadSamplesNotSF) BassMidi.BASS_MIDI_StreamLoadSamples(MainWindow.KMCGlobals._recHandle);
                        }
                        else BASSLoadSoundFonts2(ref sfnum);
                    }
                    else BASSLoadSoundFonts2(ref sfnum);
                }
            }
        }

        public static void BASSLoadSoundFonts2(ref int sfnum)
        {   
            // Prepare SoundFonts list
            Presets = new BASS_MIDI_FONTEX[MainWindow.SoundFontChain.SoundFonts.Length + 1];
            String[] SoundFontsReverse = MainWindow.SoundFontChain.SoundFonts.Reverse().ToArray();

            try
            {
                // Then load all the other SFs
                foreach (string s in SoundFontsReverse)
                {
                    if (s.ToLower().IndexOf('=') != -1)
                    {
                        var matches = System.Text.RegularExpressions.Regex.Matches(s, "[0-9]+");
                        Presets[sfnum].font = BassMidi.BASS_MIDI_FontInit(s.Substring(s.LastIndexOf('|') + 1));
                        Presets[sfnum].dbank = Convert.ToInt32(matches[0].ToString());
                        Presets[sfnum].dpreset = Convert.ToInt32(matches[1].ToString());
                        Presets[sfnum].sbank = Convert.ToInt32(matches[2].ToString());
                        Presets[sfnum].spreset = Convert.ToInt32(matches[3].ToString());
                        Presets[sfnum].dbanklsb = 0;
                        if (!Properties.Settings.Default.PreloadSamplesNotSF) BassMidi.BASS_MIDI_FontLoad(Presets[sfnum].font, Presets[sfnum].spreset, Presets[sfnum].sbank);
                        sfnum++;
                    }
                    else
                    {
                        Presets[sfnum].font = BassMidi.BASS_MIDI_FontInit(s);
                        Presets[sfnum].dpreset = -1;
                        Presets[sfnum].dbank = 0;
                        Presets[sfnum].spreset = -1;
                        Presets[sfnum].sbank = -1;
                        Presets[sfnum].dbanklsb = 0;
                        if (!Properties.Settings.Default.PreloadSamplesNotSF) BassMidi.BASS_MIDI_FontLoad(Presets[sfnum].font, Presets[sfnum].spreset, Presets[sfnum].sbank);
                        sfnum++;
                    }
                }
            }
            catch { BASSCloseStreamCrash(new InvalidSoundFont("Invalid SoundFont chain.")); }
            finally
            {
                // Always preload default SoundFont
                if (Properties.Settings.Default.PreloadDefaultSF)
                {
                    DirectoryInfo PathToGenericSF = Directory.GetParent(Assembly.GetExecutingAssembly().Location);
                    String FullPath = String.Format("{0}\\GMGeneric.sf2", PathToGenericSF.Parent.FullName);

                    LoadSpecificSoundFont(FullPath, ref sfnum);
                }

                BassMidi.BASS_MIDI_StreamSetFonts(MainWindow.KMCGlobals._recHandle, Presets, sfnum);
                if (Properties.Settings.Default.PreloadSamplesNotSF) BassMidi.BASS_MIDI_StreamLoadSamples(MainWindow.KMCGlobals._recHandle);
            }
        }

        // SF system

        private static void BASSInitializeChanAttributes()
        {
            Bass.BASS_ChannelSetAttribute(MainWindow.KMCGlobals._recHandle, BASSAttribute.BASS_ATTRIB_MIDI_VOICES, Properties.Settings.Default.Voices);
            Bass.BASS_ChannelSetAttribute(MainWindow.KMCGlobals._recHandle, BASSAttribute.BASS_ATTRIB_MIDI_CPU, 0);
            if (Properties.Settings.Default.SincInter) Bass.BASS_ChannelSetAttribute(MainWindow.KMCGlobals._recHandle, BASSAttribute.BASS_ATTRIB_SRC, 3);
        }

        public static void BASSVolumeSlideInit()
        {
            MainWindow.KMCGlobals._VolFX = Bass.BASS_ChannelSetFX((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), BASSFXType.BASS_FX_VOLUME, 1);
            MainWindow.KMCGlobals._VolFXParam.fCurrent = 1.0f;
            MainWindow.KMCGlobals._VolFXParam.fTarget = Properties.Settings.Default.Volume;
            MainWindow.KMCGlobals._VolFXParam.fTime = 0.0f;
            MainWindow.KMCGlobals._VolFXParam.lCurve = 0;
            Bass.BASS_FXSetParameters(MainWindow.KMCGlobals._VolFX, MainWindow.KMCGlobals._VolFXParam);
        }

        public static void BASSStreamSystem(String str, Boolean PreviewMode)
        {
            try
            {
                MainWindow.KMCGlobals._recHandle = BassMidi.BASS_MIDI_StreamCreateFile(str, 0L, 0L,
                    BASSFlag.BASS_STREAM_DECODE |
                    (PreviewMode ? 0 : (Properties.Settings.Default.SincInter ? BASSFlag.BASS_MIDI_SINCINTER : 0)) |
                    BASSFlag.BASS_SAMPLE_FLOAT |
                    BASSFlag.BASS_SAMPLE_SOFTWARE,
                    0);

                if (PreviewMode)
                {
                    BASS_WASAPI_INFO infoW = new BASS_WASAPI_INFO();

                    BassWasapi.BASS_WASAPI_Init(-1, 0, 2, BASSWASAPIInit.BASS_WASAPI_BUFFER, 0, 0, null, IntPtr.Zero);
                    BassWasapi.BASS_WASAPI_GetInfo(infoW);
                    BassWasapi.BASS_WASAPI_Free();

                    MainWindow.KMCGlobals._myWasapi = new WASAPIPROC(MyWasapiProc);
                    BassWasapi.BASS_WASAPI_Init(-1, 0, 2, BASSWASAPIInit.BASS_WASAPI_EVENT, infoW.buflen, 0, MainWindow.KMCGlobals._myWasapi, IntPtr.Zero);

                    if (Bass.BASS_ErrorGetCode() != 0) throw new Exception("Can not initialize WASAPI.");
                }

                BASSInitializeChanAttributes();
                BASSTempoAndFilter();

                if (Path.GetFileNameWithoutExtension(str).Length >= 49)
                    MainWindow.KMCGlobals.NewWindowName = Path.GetFileNameWithoutExtension(str).Truncate(45);
                else
                    MainWindow.KMCGlobals.NewWindowName = Path.GetFileNameWithoutExtension(str);

                MainWindow.KMCGlobals._plm = new Un4seen.Bass.Misc.DSP_PeakLevelMeter(MainWindow.KMCGlobals._recHandle, 1);
                MainWindow.KMCGlobals._plm.CalcRMS = true;
            }
            catch (Exception ex)
            {
                BASSCloseStreamCrash(ex);
            }
        }

        public static void BASSStreamSystemRT(String str, Boolean PreviewMode)
        {
            try
            {
                MainWindow.KMCGlobals._recHandle = BassMidi.BASS_MIDI_StreamCreateFile(str, 0L, 0L, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_SAMPLE_FLOAT, 0);

                BASS_MIDI_EVENT[] eventChunk;
                try
                {
                    // Thank you so much Falcosoft for helping me there!!!
                    // Visit his website: http://falcosoft.hu/index.html#start
                    MainWindow.KMCGlobals.eventc = (UInt32)BassMidi.BASS_MIDI_StreamGetEvents(MainWindow.KMCGlobals._recHandle, -1, 0, null); // Counts all the events in the MIDI
                    MainWindow.KMCGlobals.events = new BASS_MIDI_EVENT[MainWindow.KMCGlobals.eventc]; // Creates an array with the events count as size
                    for (int i = 0; i <= (MainWindow.KMCGlobals.eventc / 50000000); i++)
                    {
                        int subCount = Math.Min(50000000, (int)MainWindow.KMCGlobals.eventc - (i * 50000000));
                        eventChunk = new BASS_MIDI_EVENT[subCount];
                        BassMidi.BASS_MIDI_StreamGetEvents(MainWindow.KMCGlobals._recHandle, -1, 0, eventChunk, i * 50000000, subCount); //Falcosoft: to avoid marshalling errors pass the smaller local buffer to the function   
                        eventChunk.CopyTo(MainWindow.KMCGlobals.events, i * 50000000); //Falcosoft: copy local buffer to global one at each iteration
                    }
                    eventChunk = null;
                }
                catch
                {
                    BASSCloseStreamCrash(new MIDILoadError("This MIDI is too big for the real-time conversion process.\n\nMake sure you're using the 64-bit version of the converter."));
                }

                Bass.BASS_StreamFree(MainWindow.KMCGlobals._recHandle);
                MainWindow.KMCGlobals._recHandle = BassMidi.BASS_MIDI_StreamCreate(16, 
                    BASSFlag.BASS_STREAM_DECODE | 
                    (PreviewMode ? 0 : (Properties.Settings.Default.SincInter ? BASSFlag.BASS_MIDI_SINCINTER : 0)) |
                    BASSFlag.BASS_SAMPLE_FLOAT | 
                    BASSFlag.BASS_SAMPLE_SOFTWARE,
                    0);

                BASSInitializeChanAttributes();
                BASSTempoAndFilter();

                if (Path.GetFileNameWithoutExtension(str).Length >= 49)
                    MainWindow.KMCGlobals.NewWindowName = Path.GetFileNameWithoutExtension(str).Truncate(45);
                else
                    MainWindow.KMCGlobals.NewWindowName = Path.GetFileNameWithoutExtension(str);
                MainWindow.KMCGlobals._plm = new Un4seen.Bass.Misc.DSP_PeakLevelMeter(MainWindow.KMCGlobals._recHandle, 1);
                MainWindow.KMCGlobals._plm.CalcRMS = true;
            }
            catch (Exception ex)
            {
                BASSCloseStreamCrash(ex);
            }
        }

        public static void BASSTempoAndFilter()
        {
            SetTempo(true, true);
            MainWindow.KMCGlobals._myTempoSync = new SYNCPROC(TempoSync);
            Bass.BASS_ChannelSetSync(MainWindow.KMCGlobals._recHandle, BASSSync.BASS_SYNC_MIDI_EVENT | BASSSync.BASS_SYNC_MIXTIME, (long)BASSMIDIEvent.MIDI_EVENT_TEMPO, MainWindow.KMCGlobals._myTempoSync, IntPtr.Zero);
            Bass.BASS_ChannelSetSync(MainWindow.KMCGlobals._recHandle, BASSSync.BASS_SYNC_SETPOS | BASSSync.BASS_SYNC_MIXTIME, 0, MainWindow.KMCGlobals._myTempoSync, IntPtr.Zero);
            if (Properties.Settings.Default.IgnoreNotes1 || Properties.Settings.Default.AskForIgnoreTracks)
            {
                Int32 TracksCount = BassMidi.BASS_MIDI_StreamGetTrackCount(MainWindow.KMCGlobals._recHandle);

                if (Properties.Settings.Default.AskForIgnoreTracks) new TracksToIgnore(TracksCount).ShowDialog();
                else TracksList = new bool[TracksCount];

                MainWindow.KMCGlobals._FilterProc = new MIDIFILTERPROC(MidiFilterProc);
                BassMidi.BASS_MIDI_StreamSetFilter(MainWindow.KMCGlobals._recHandle, true, MainWindow.KMCGlobals._FilterProc, IntPtr.Zero);
            }
        }

        public static void BASSEffectSettings()
        {
            if (Properties.Settings.Default.DisableEffects == true)
                Bass.BASS_ChannelFlags((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), BASSFlag.BASS_MIDI_NOFX, BASSFlag.BASS_MIDI_NOFX);
            else
                Bass.BASS_ChannelFlags((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), 0, BASSFlag.BASS_MIDI_NOFX);

            if (Properties.Settings.Default.NoteOff1 == true)
                Bass.BASS_ChannelFlags((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), BASSFlag.BASS_MIDI_NOTEOFF1, BASSFlag.BASS_MIDI_NOTEOFF1);
            else
                Bass.BASS_ChannelFlags((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), 0, BASSFlag.BASS_MIDI_NOTEOFF1);
        }

        public static BASSEncode IsOgg(int format)
        {
            switch (format)
            {
                case 0:
                    return BASSEncode.BASS_ENCODE_PCM;
                default:
                    return (BASSEncode)0;
            }
        }

        public static BASSEncode IsWav(int format)
        {
            switch (format)
            {
                case 0:
                    return BASSEncode.BASS_ENCODE_RF64;
                default:
                    return (BASSEncode)0;
            }
        }

        public static string EncoderString(String encpath, String str, String ext, String argstoadd)
        {
            if (ext == "wav")
            {
                return String.Format("{0}.{1}", str, ext);
            }
            else
            {
                return String.Format("{0} {1} \"{2}.{3}\"", encpath, argstoadd, str, ext);
            }
        }

        public static void BASSEncoderInit(Int32 format, String str)
        {
            try
            {
                string pathwithoutext = String.Format("{0}\\{1}", Properties.Settings.Default.LastExportFolder, Path.GetFileNameWithoutExtension(str));
                string ext;
                string enc;
                string args;
                int copynum = 1;
                if (format == 1)
                {
                    foreach (Process proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Program.OGGEnc)))
                    {
                        proc.Kill();
                    }
                    ext = "ogg";
                    enc = Program.OGGEnc;
                    args = String.Format(Properties.Settings.Default.EncoderOGG, Properties.Settings.Default.OverrideBitrate ? Properties.Settings.Default.Bitrate : 192);
                }
                else if (format == 2)
                {
                    foreach (Process proc in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Program.MP3Enc)))
                    {
                        proc.Kill();
                    }
                    ext = "mp3";
                    enc = Program.MP3Enc;
                    args = String.Format(Properties.Settings.Default.EncoderMP3, Properties.Settings.Default.OverrideBitrate ? Properties.Settings.Default.Bitrate : 192);
                }
                else
                {
                    ext = "wav";
                    enc = null;
                    args = "";
                }
                if (File.Exists(String.Format("{0}.{1}", pathwithoutext, ext)))
                {
                    string temp;
                    do
                    {
                        temp = String.Format("{0} ({1} {2})", pathwithoutext, Languages.Parse("CopyText"), copynum);
                        ++copynum;
                    } while (File.Exists(String.Format("{0}.{1}", temp, ext)));
                    BassEnc.BASS_Encode_Stop(MainWindow.VSTs._VSTHandles[0]);
                    BassEnc.BASS_Encode_Stop(MainWindow.KMCGlobals._recHandle);
                    MainWindow.KMCGlobals._Encoder = BassEnc.BASS_Encode_Start((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), EncoderString(enc, temp, ext, args), (Properties.Settings.Default.LoudMaxEnabled ? BASSEncode.BASS_ENCODE_FP_16BIT : BASSEncode.BASS_ENCODE_DEFAULT) | BASSEncode.BASS_ENCODE_AUTOFREE | IsOgg(format) | IsWav(format), null, IntPtr.Zero);
                }
                else
                {
                    BassEnc.BASS_Encode_Stop(MainWindow.VSTs._VSTHandles[0]);
                    BassEnc.BASS_Encode_Stop(MainWindow.KMCGlobals._recHandle);
                    MainWindow.KMCGlobals._Encoder = BassEnc.BASS_Encode_Start((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), EncoderString(enc, pathwithoutext, ext, args), (Properties.Settings.Default.LoudMaxEnabled ? BASSEncode.BASS_ENCODE_FP_16BIT : BASSEncode.BASS_ENCODE_DEFAULT) | BASSEncode.BASS_ENCODE_AUTOFREE | IsOgg(format) | IsWav(format), null, IntPtr.Zero);
                }
            }
            catch (Exception ex)
            {
                BASSCloseStreamCrash(ex);
            }
        }

        public static bool BASSEncodingEngine(long pos, int length)
        {
            int tempo = BassMidi.BASS_MIDI_StreamGetEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_TEMPO);
            MainWindow.KMCGlobals.OriginalTempo = 60000000 / tempo;
            byte[] buffer = new byte[length];

            for (int i = 0; i <= 15; i++)
                BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, i, BASSMIDIEvent.MIDI_EVENT_MIXLEVEL, MainWindow.KMCStatus.ChannelsVolume[i]);

            if (MainWindow.VSTs.VSTInfo[0].isInstrument) Bass.BASS_ChannelGetData(MainWindow.VSTs._VSTHandles[0], buffer, length);
            int got = Bass.BASS_ChannelGetData(MainWindow.KMCGlobals._recHandle, buffer, length);

            if (got < 0)
            {
                MainWindow.KMCGlobals.CancellationPendingValue = 2;
                return false;
            }

            return true;
        }

        public static bool BASSEncodingEngineRT(double[] CustomFramerates, ref int pos, ref uint es)
        {
            double fpssim = MainWindow.FPSSimulator.NextDouble() * (CustomFramerates[0] - CustomFramerates[1]) + CustomFramerates[1];
            int length = Convert.ToInt32(Bass.BASS_ChannelSeconds2Bytes(MainWindow.KMCGlobals._recHandle, fpssim));
            byte[] buffer = new byte[length];

            for (int i = 0; i <= 15; i++)
                BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, i, BASSMIDIEvent.MIDI_EVENT_MIXLEVEL, MainWindow.KMCStatus.ChannelsVolume[i]);

            while (es < MainWindow.KMCGlobals.eventc && MainWindow.KMCGlobals.events[es].pos < pos + length)
            {
                BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, MainWindow.KMCGlobals.events[es].chan, MainWindow.KMCGlobals.events[es].eventtype, MainWindow.KMCGlobals.events[es].param);
                es++;
            }

            if (MainWindow.VSTs.VSTInfo[0].isInstrument) Bass.BASS_ChannelGetData(MainWindow.VSTs._VSTHandles[0], buffer, length);
            int got = Bass.BASS_ChannelGetData(MainWindow.KMCGlobals._recHandle, buffer, length);

            if (got < 0)
            {
                MainWindow.KMCGlobals.CancellationPendingValue = 2;
                return false;
            }

            pos += got;
            if (es == MainWindow.KMCGlobals.eventc) BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_END, 0);

            float fpsstring = 1 / (float)fpssim;

            return true;
        }

        public static int BASSPlayBackEngine(int notes, int length, long pos)
        {
            int pnotes = notes;
            int tempo = BassMidi.BASS_MIDI_StreamGetEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_TEMPO);
            MainWindow.KMCGlobals.OriginalTempo = 60000000 / tempo;

            if (Properties.Settings.Default.DisableEffects == true)
                Bass.BASS_ChannelFlags(MainWindow.KMCGlobals._recHandle, BASSFlag.BASS_MIDI_NOFX, BASSFlag.BASS_MIDI_NOFX);
            else
                Bass.BASS_ChannelFlags(MainWindow.KMCGlobals._recHandle, 0, BASSFlag.BASS_MIDI_NOFX);
            if (Properties.Settings.Default.NoteOff1 == true)
                Bass.BASS_ChannelFlags(MainWindow.KMCGlobals._recHandle, BASSFlag.BASS_MIDI_NOTEOFF1, BASSFlag.BASS_MIDI_NOTEOFF1);
            else
                Bass.BASS_ChannelFlags(MainWindow.KMCGlobals._recHandle, 0, BASSFlag.BASS_MIDI_NOTEOFF1);

            for (int i = 0; i <= 15; i++)
                BassMidi.BASS_MIDI_StreamEvent((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), i, BASSMIDIEvent.MIDI_EVENT_MIXLEVEL, MainWindow.KMCStatus.ChannelsVolume[i]);

            MainWindow.KMCGlobals._VolFXParam.fCurrent = 1.0f;
            MainWindow.KMCGlobals._VolFXParam.fTarget = Properties.Settings.Default.Volume;
            MainWindow.KMCGlobals._VolFXParam.fTime = 0.0f;
            MainWindow.KMCGlobals._VolFXParam.lCurve = 0;
            Bass.BASS_FXSetParameters(MainWindow.KMCGlobals._VolFX, MainWindow.KMCGlobals._VolFXParam);

            Bass.BASS_ChannelSetAttribute(MainWindow.KMCGlobals._recHandle, BASSAttribute.BASS_ATTRIB_MIDI_VOICES, Properties.Settings.Default.Voices);

            if (MainWindow.Seeking)
            {
                BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_NOTESOFF, 0);
                Bass.BASS_ChannelSetPosition(MainWindow.KMCGlobals._recHandle, MainWindow.CurrentSeek, BASSMode.BASS_POS_MIDI_TICK);
                MainWindow.Seeking = false;
            }

            Thread.Sleep(1);

            if (!MainWindow.KMCGlobals.DoNotCountNotes)
                return pnotes;
            else
                return 0;
        }

        public static bool BASSPlayBackEngineRT(double[] CustomFramerates, ref int pos, ref uint es)
        {
            int tempo = BassMidi.BASS_MIDI_StreamGetEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_TEMPO);
            MainWindow.KMCGlobals.OriginalTempo = 60000000 / tempo;

            BASSEffectSettings();

            for (int i = 0; i <= 15; i++)
                BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, i, BASSMIDIEvent.MIDI_EVENT_MIXLEVEL, MainWindow.KMCStatus.ChannelsVolume[i]);

            MainWindow.KMCGlobals._VolFXParam.fCurrent = 1.0f;
            MainWindow.KMCGlobals._VolFXParam.fTarget = Properties.Settings.Default.Volume;
            MainWindow.KMCGlobals._VolFXParam.fTime = 0.0f;
            MainWindow.KMCGlobals._VolFXParam.lCurve = 0;
            Bass.BASS_FXSetParameters(MainWindow.KMCGlobals._VolFX, MainWindow.KMCGlobals._VolFXParam);

            if (MainWindow.Seeking)
            {
                Bass.BASS_ChannelSetPosition(MainWindow.KMCGlobals._recHandle, MainWindow.CurrentSeek, BASSMode.BASS_POS_MIDI_TICK);
                MainWindow.Seeking = false;
            }

            double fpssim = MainWindow.FPSSimulator.NextDouble() * (CustomFramerates[0] - CustomFramerates[1]) + CustomFramerates[1];
            int length = Convert.ToInt32(Bass.BASS_ChannelSeconds2Bytes((MainWindow.VSTs.VSTInfo[0].isInstrument ? MainWindow.VSTs._VSTHandles[0] : MainWindow.KMCGlobals._recHandle), fpssim));
            byte[] buffer = new byte[length];

            for (int i = 0; i <= 15; i++) BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, i, BASSMIDIEvent.MIDI_EVENT_MIXLEVEL, MainWindow.KMCStatus.ChannelsVolume[i]);

            while (es < MainWindow.KMCGlobals.eventc && MainWindow.KMCGlobals.events[es].pos < pos + length)
            {
                BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, MainWindow.KMCGlobals.events[es].chan, MainWindow.KMCGlobals.events[es].eventtype, MainWindow.KMCGlobals.events[es].param);
                es++;
            }

            int got;
            if (MainWindow.VSTs.VSTInfo[0].isInstrument)
            {
                got = Bass.BASS_ChannelGetData(MainWindow.KMCGlobals._recHandle, buffer, length);
                Bass.BASS_ChannelGetData(MainWindow.VSTs._VSTHandles[0], buffer, length);
            }
            else got = Bass.BASS_ChannelGetData(MainWindow.KMCGlobals._recHandle, buffer, length);

            if (got < 0)
            {
                MainWindow.KMCGlobals.CancellationPendingValue = 2;
                return false;
            }
            pos += got;

            if (es == MainWindow.KMCGlobals.eventc) BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_END, 0);

            float fpsstring = 1 / (float)fpssim;

            return true;
        }

        public static void SetTempo(bool reset, bool isitworker)
        {
            if (isitworker)
            {
                if (reset) MainWindow.KMCGlobals.MIDITempo = BassMidi.BASS_MIDI_StreamGetEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_TEMPO); // get the file's tempo
                BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_TEMPO, Convert.ToInt32(MainWindow.KMCGlobals.MIDITempo * MainWindow.KMCGlobals.TempoScale));   // set tempo
            }
            else
            {
                new Thread(() =>
                {
                    if (reset) MainWindow.KMCGlobals.MIDITempo = BassMidi.BASS_MIDI_StreamGetEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_TEMPO); // get the file's tempo
                    BassMidi.BASS_MIDI_StreamEvent(MainWindow.KMCGlobals._recHandle, 0, BASSMIDIEvent.MIDI_EVENT_TEMPO, Convert.ToInt32(MainWindow.KMCGlobals.MIDITempo * MainWindow.KMCGlobals.TempoScale));   // set tempo
                }).Start();
            }
        }
    }
}

// Custom exceptions
public class MIDILoadError : Exception
{
    public MIDILoadError()
    {
    }

    public MIDILoadError(string message)
        : base(message)
    {
    }

    public MIDILoadError(string message, Exception inner)
        : base(message, inner)
    {
    }
}

public class InvalidSoundFont : Exception
{
    public InvalidSoundFont()
    {
    }

    public InvalidSoundFont(string message)
        : base(message)
    {
    }

    public InvalidSoundFont(string message, Exception inner)
        : base(message, inner)
    {
    }
}