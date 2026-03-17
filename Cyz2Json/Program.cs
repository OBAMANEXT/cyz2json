﻿//
// Cyz2Json
//
// Convert CYZ flow cytometry files to a JSON format.
//
// Copyright(c) 2023 Centre for Environment, Fisheries and Aquaculture Science.
//

using CytoSense.CytoSettings;
using CytoSense.Data;
using CytoSense.Data.Analysis;
using CytoSense.Data.ParticleHandling;
using CytoSense.Data.ParticleHandling.Channel;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OpenCvSharp;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;


// Design decisions:
//
// We want to easily eyeball files so we choose JSON rather than a
// binary format.
//
// We want all data in one file, so we include the images as base64
// encoded chunks within the JSON rather than write them to separate
// files.
//
// We don't use the Cytobuoy provided image crop, prefering to do that
// ourselves later and giving us the option of other toolkits. This also
// allows us to drop OpenCVSharp which seems problematic for cross
// platform support.
//
// Modification by Joe Ribeiro Dec 2024: The library didn't export 
// gain setting relating to the photomultiplier tubes. This means
// the amplitudes can be very different from different machines with 
// different PMT settings. I suspect there will be other settings we 
// discover like this, so to me it makes sense to retain any instrument
// setting we can grab, which is what this modification does by default.
// Make the .json files as they previously were in the last commit with:
// --metadatagreedy false

namespace Cyz2Json
{
    internal class Program
    {
        static void HandleVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            Console.WriteLine($"Cyz2Json version {informationalVersion ?? version?.ToString()}");
        }

        static void Main(string[] args)
        {
            var inputArgument         = new Argument<FileInfo>( name: "input",                     description: "CYZ input file");
            var outputOption          = new Option<FileInfo>(   name: "--output",                  description: "JSON output file");
            var rawOption             = new Option<bool>(       name: "--raw",                     description: "Do not apply the moving weighted average filtering algorithm to pulse shapes. Export raw, unsmoothed data.");
            var metadatagreedyOption  = new Option<bool>(       name: "--metadatagreedy",          description: "Save all possible measurement settings with your file (default: true)", getDefaultValue: () => true);
            var versionOption         = new Option<bool>(       name: "-V",                        description: "Display version information");
            var setInformationOption  = new Option<bool>(       name: "--imaging-set-information", description: "Export set information for imaging" );
            var setDefinitionOverride = new Option<FileInfo>(   name: "--imaging-set-definition",  description: "File with set definitions, overrides the definitions stored in the file." ).ExistingOnly();

            setDefinitionOverride.AddValidator( result  => { 
                if ( ! result.GetValueForOption(setInformationOption) ) {
                    result.ErrorMessage = "Specifying a region information file is only useful when exporting region information.";
                }
            });

            var imageProcessing = new Option<bool>( name: "--image-processing", description: "Perform cropping and image processing during the export of the image.", getDefaultValue: () => false                );

            var imageProcessingThreshold             = new Option<int>(  name: "--image-processing-threshold",               description: "The minimum pixel value difference from the background to be considered an object.", getDefaultValue: () => 9);
            var imageProcessingErosionDilation       = new Option<int>(  name: "--image-processing-erosion-dilation",        description: "The size of the erosion/dilation filter to apply after thresholding.",               getDefaultValue: () => 1);
            var imageProcessingBrightFieldCorrection = new Option<bool>( name: "--image-processing-bright-field-correction", description: "Correct the image for variation in the lighting.",                                   getDefaultValue: () => true);
            var imageProcessingMarginBase            = new Option<int>(  name: "--image-processing-margin-base",             description: "Add a marging of this many pixels around the detected object.",                      getDefaultValue: () => 25 );
            var imageProcessingMarginPercentage      = new Option<int>(  name: "--image-processing-margin-percentage",       description: "Add an extra margin that is a percentage of the size of the detected object.",       getDefaultValue: () => 10 );
            var imageProcessingExtendObjectDetection = new Option<bool>( name: "--image-processing-extend-object-detection", description: "When seperate objects are detected close (in the margin) of the main object then extend the rectangle to include these objects as well.", getDefaultValue: () => true );



            var rootCommand = new RootCommand("Convert CYZ files to JSON")
            {
                inputArgument, outputOption, rawOption, metadatagreedyOption, versionOption, 
                setInformationOption, setDefinitionOverride,
                imageProcessing,
                imageProcessingThreshold, imageProcessingErosionDilation,
                imageProcessingBrightFieldCorrection,
                imageProcessingMarginBase, imageProcessingMarginPercentage,
                imageProcessingExtendObjectDetection
            };

            rootCommand.TreatUnmatchedTokensAsErrors = false;

            ParseResult parseResult = rootCommand.Parse(args);
            if (parseResult.Errors.Count == 0)
            {
                FileInfo input                    = parseResult.GetValueForArgument(inputArgument);
                FileInfo output                   = parseResult.GetValueForOption(outputOption)!;
                bool     raw                      = parseResult.GetValueForOption(rawOption)!;
                bool     metadatagreedy           = parseResult.GetValueForOption(metadatagreedyOption)!;
                bool     version                  = parseResult.GetValueForOption(versionOption)!;
                bool     setInformation           = parseResult.GetValueForOption(setInformationOption)!;
                FileInfo setDefinitionFile        = parseResult.GetValueForOption(setDefinitionOverride)!;
                bool     processImages            = parseResult.GetValueForOption(imageProcessing);
                int      imgThreshold             = parseResult.GetValueForOption(imageProcessingThreshold);
                int      imgErosionDilation       = parseResult.GetValueForOption(imageProcessingErosionDilation);
                bool     imgBrightFieldCorrection = parseResult.GetValueForOption(imageProcessingBrightFieldCorrection);
                int      imgMarginBase            = parseResult.GetValueForOption(imageProcessingMarginBase);
                int      imgMarginPercentage      = parseResult.GetValueForOption(imageProcessingMarginPercentage);
                bool     imgExtendObjectDetection = parseResult.GetValueForOption(imageProcessingExtendObjectDetection);


                ImageProcessingOptions imgOpts = new() { Process=processImages, Threshold=imgThreshold, ErosionDilation=imgErosionDilation, BrightFieldCorrection=imgBrightFieldCorrection,
                                                         MarginBase=imgMarginBase, MarginPercentage=imgMarginPercentage, ExtendObjectDetection=imgExtendObjectDetection};


                Convert(input, output, raw, metadatagreedy, setInformation, setDefinitionFile, imgOpts);
            }
            foreach (ParseError parseError in parseResult.Errors)
            {
                Console.Error.WriteLine(parseError.Message);
            }
            // rootCommand.Invoke(args);
        }

        private record struct ImageProcessingOptions(bool Process, int Threshold, int ErosionDilation, bool BrightFieldCorrection, int MarginBase, int MarginPercentage, bool ExtendObjectDetection);

        private static CytoImage.ImageProcessingSettings ToImgProcSettings(ImageProcessingOptions options)
        {
            return new CytoImage.ImageProcessingSettings {
                Threshold                  = options.Threshold,
                ErosionDilation            = options.ErosionDilation,
                ApplyBrightFieldCorrection = options.BrightFieldCorrection,
                MarginBase                 = options.MarginBase,
                MarginFactor               = options.MarginPercentage/100.0,
                ExtendObjectDetection      = options.ExtendObjectDetection
            };
        }

        /// <summary>
        /// Convert the flow cytometry data in the file designated by cyzFilename to Javscript Object Notation (JSON).
        /// If jsonFilename is null, echo the JSON to the console, otherwise store it a file designated by jsonFilename.
        /// </summary>
        static void Convert(FileInfo cyzFilename, FileInfo jsonFilename, bool isRaw, bool metadatagreedy, bool setInformation, FileInfo setDefinitionFile, ImageProcessingOptions imgOpts)
        {
            var data = LoadData(cyzFilename.FullName, isRaw, metadatagreedy, setInformation, setDefinitionFile, imgOpts);

            StreamWriter streamWriter;

            if (jsonFilename is null)
                streamWriter = new StreamWriter(Console.OpenStandardOutput());
            else
                streamWriter = File.CreateText(jsonFilename.FullName);

            using (JsonTextWriter writer = new JsonTextWriter(streamWriter))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(writer, data);
            }

            streamWriter.Close();
        }

        /// <summary>
        /// Load all the flow cytometry data from the CYZ file designated by pathname.
        /// </summary>
        private static Dictionary<string, object> LoadData(string pathname, bool isRaw, bool metadatagreedy, bool setInformation, FileInfo setDefinitionFile, ImageProcessingOptions imgOpts)
        {
            var data = new Dictionary<string, object>();

            data["filename"] = Path.GetFileName(pathname);

            var dfw = new DataFileWrapper(pathname);

            dfw.CytoSettings.setChannelVisualisationMode(ChannelAccessMode.Optical_debugging);
            ChannelData.VarLength = 13; // Calculate the alternate variable height length parameter at a height of 13%

            // Some old files can have a problem where the pre-concentration
            // measurement and the actual concentration during the measurement
            // disagree.  If this happens and you try to use the data this will
            // result in an exception.  You can check this beforehand, and force the
            // use of one of the concentrations during the calculations.

            double concentration = 0.0;
            double preconcentration = 0.0;
            if (dfw.CheckConcentration(ref concentration, ref preconcentration))
            {
                // In this case usually the pre-concentration is the correct one, so we use that.

                Console.WriteLine("WARNING: concentration measurement disagrees with pre-concentration measurement. Using the latter.");
                dfw.ConcentrationMode = ConcentrationModeEnum.Pre_measurement_FTDI;
            }
            data["instrument"] = LoadInstrument(dfw, metadatagreedy);

            SetInformation regInfo;
            SetsList ? sets = null;
            if (setInformation) {
                (regInfo, sets) = SetInformation.LoadImagingSetInformation(dfw, setDefinitionFile);
                data["set_information"] = regInfo;
            }

            data["particles"] = LoadParticles(dfw, isRaw, sets);
            data["images"]    = LoadImages(dfw, imgOpts);

            return data;
        }


        private static Dictionary<string, object> LoadInstrument(DataFileWrapper dfw, bool metadatagreedy)
        {
            var instrument = new Dictionary<string, object>();

            instrument["name"] = dfw.CytoSettings.name;
            instrument["serialNumber"] = dfw.CytoSettings.SerialNumber;
            instrument["sampleCoreSpeed"] = dfw.CytoSettings.SampleCorespeed;
            instrument["laserBeamWidth"] = dfw.CytoSettings.LaserBeamWidth;
            instrument["channels"] = LoadChannels(dfw);
            instrument["measurementSettings"] = LoadMeasurementSettings(dfw, metadatagreedy);
            instrument["measurementResults"] = LoadMeasurementResults(dfw);

            return instrument;
        }

        private static Dictionary<string, object> LoadMeasurementSettings(DataFileWrapper dfw, bool metadatagreedy)
        {
            if (metadatagreedy)
            {
                var measurementInstrumentSettings = new Dictionary<string, object>();

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new IgnoreErrorPropertiesResolver(),
                    Error = (sender, args) =>
                    {
                        args.ErrorContext.Handled = true;
                    }
                };

                var cytoSettingsJson = JsonConvert.SerializeObject(dfw.MeasurementSettings, settings);
                var cytoSettings = JsonConvert.DeserializeObject(cytoSettingsJson);

                if (cytoSettings != null) {
                    measurementInstrumentSettings["CytoSettings"] = cytoSettings;
                }
                return measurementInstrumentSettings;
            }
            else
            {
                var measurementSettings = new Dictionary<string, object>();

                measurementSettings["name"] = dfw.MeasurementSettings.TabName;
                measurementSettings["duration"] = dfw.MeasurementSettings.StopafterTimertext; // seconds
                measurementSettings["pumpSpeed"] = dfw.MeasurementSettings.ConfiguredSamplePompSpeed; // muL/s
                measurementSettings["triggerChannel"] = dfw.MeasurementSettings.TriggerChannel;
                measurementSettings["triggerLevel"] = dfw.MeasurementSettings.TriggerLevel1e;
                measurementSettings["smartTrigger"] = dfw.MeasurementSettings.SmartTriggeringEnabled;
                
                if (dfw.MeasurementSettings.SmartTriggeringEnabled)
                    measurementSettings["smartTriggerDescription"] = dfw.MeasurementSettings.SmartTriggerSettingDescription;
                    
                measurementSettings["takeImages"] = dfw.MeasurementSettings.IIFCheck;
                measurementSettings["PMTlevels_str"] = dfw.MeasurementSettings.PMTlevels_str;
                measurementSettings["sensorLimits"] = dfw.CytoSettings.SensorLimits;

                // VLIZ version (all those data should be in metadatagreedy = true)
                // measurementSettings["name"] = dfw.MeasurementSettings.TabName;
                // measurementSettings["duration"] = dfw.MeasurementSettings.StopafterTimertext;
                // measurementSettings["pumpSpeed"] = dfw.MeasurementSettings.ConfiguredSamplePompSpeed;
                // measurementSettings["triggerChannel"] = dfw.MeasurementSettings.TriggerChannel;
                // measurementSettings["triggerLevel"] = dfw.MeasurementSettings.TriggerLevel1e;
                // measurementSettings["smartTrigger"] = dfw.MeasurementSettings.SmartTriggeringEnabled;

                // measurementSettings["easy_display_cytoclus"] = dfw.MeasurementSettings.PMTlevels_str;
                // measurementSettings["sampling_time_s"] = dfw.MeasurementSettings.StopafterTimertext; // seconds
                // measurementSettings["sample_pump_ul_s"] = dfw.MeasurementSettings.ConfiguredSamplePompSpeed; // muL/s
                // measurementSettings["limit_particle_rate_s"] = dfw.MeasurementSettings.MaxParticleRate;
                // measurementSettings["minimum_speed_ul_s"] = dfw.MeasurementSettings.MinimumAutoSpeed;
                // measurementSettings["flush"] = dfw.MeasurementSettings.FlushCheck;
                // measurementSettings["beads_measurement_2"] = dfw.MeasurementSettings.IsBeadsMeasurement;
                // if (dfw.MeasurementSettings.SmartTriggeringEnabled)
                    // measurementSettings["smart_trigger"]           = dfw.MeasurementSettings.SmartTriggerSettingDescription; // VELIZ line
                    // measurementSettings["smartTriggerDescription"] = dfw.MeasurementSettings.SmartTriggerSettingDescription; // Joseph Ribeiro line

                // measurementSettings["takeImages"] = dfw.MeasurementSettings.IIFCheck;
                // measurementSettings["images_in_flow"] = dfw.CytoSettings.hasImageAndFlow;
                // measurementSettings["speak_when_finished"] = dfw.MeasurementSettings.TellCheck;
                // measurementSettings["enable_images_in_flow"] = dfw.MeasurementSettings.IIFCheck;
                // measurementSettings["maximum_images_in_flow"] = dfw.MeasurementSettings.MaxNumberFotoText;
                // measurementSettings["ROI"] = dfw.MeasurementSettings.IIFRoiName;
                // measurementSettings["restrict_FWS_min"] = dfw.MeasurementSettings.IIFFwsRatioMin;
                // measurementSettings["restrict_FWS_max"] = dfw.MeasurementSettings.IIFFwsRatioMax;
                // measurementSettings["measurement_noise_levels"] = dfw.MeasurementSettings.MeasureNoiseLevels;
                // measurementSettings["target_mode"] = dfw.MeasurementSettings.IIFuseTargetAll;
                // measurementSettings["adaptive_MaxTimeOut"] = dfw.MeasurementSettings.AdaptiveMaxTimeOut;
                // measurementSettings["adaptive_MaxTimeOut3"] = dfw.MeasurementSettings.MaxTimeOut_str;
                // measurementSettings["enable_export"] = dfw.MeasurementSettings.EnableExport;
                // measurementSettings["IIFuseSmartGrid"] = dfw.MeasurementSettings.IIFuseSmartGrid; //smartgrid use yes no
                // measurementSettings["SmartTriggeringEnabled"] = dfw.MeasurementSettings.SmartTriggeringEnabled;
                // measurementSettings["SmartGrid_str"] = dfw.MeasurementSettings.SmartGrid_str;  //string with name of all channels used for smartgrid
                // measurementSettings["TriggerChannel"] = dfw.MeasurementSettings.TriggerChannel;
                // measurementSettings["TriggerLevel1e"] = dfw.MeasurementSettings.TriggerLevel1e;       // Trigger level in mv. for the first grabber board.
                // measurementSettings["SelectedIifMode"] = dfw.MeasurementSettings.SelectedIifMode;

                //measurementSettings["all"] = dfw.MeasurementSettings;
                            
                return measurementSettings;
            }
        }


        private static List<Dictionary<string, object>> LoadParticles(DataFileWrapper dfw, bool isRaw, SetsList ? sets)
        {
            var particles = new List<Dictionary<string, object>>();

            foreach (var particle in dfw.SplittedParticles)
                particles.Add(LoadParticle(particle, isRaw, SetInformation.LoadSetNames(particle, sets)));

            return particles;
        }

        private static Dictionary<string, object> LoadParticle(Particle particle, bool isRaw, List<string> ? particleRegions)
        {

            var particleData = new Dictionary<string, object>();

            particleData["particleId"] = particle.ID;
            particleData["hasImage"] = particle.hasImage;

            if (particleRegions != null) {
                particleData["region"] = particleRegions;
            }

            // Pulse shapes

            var pulseShapes = new List<Dictionary<string, object>>();

            foreach (ChannelData cd in particle.ChannelData)
            {
                var pulseShape = new Dictionary<string, object>();

                pulseShape["description"] = cd.Information.Description;

                if (isRaw)
                    pulseShape["values"] = cd.Data_mV_unsmoothed;
                else
                    pulseShape["values"] = cd.Data;

                pulseShapes.Add(pulseShape);
            }

            particleData["pulseShapes"] = pulseShapes;

            // Parameters

            var parameters = new List<Dictionary<string, object>>();

            foreach (ChannelData cd in particle.ChannelData)
            {
                var parameter = new Dictionary<string, object>();

                parameter["description"] = cd.Information.Description;
                parameter["length"] = cd.get_Parameter(ChannelData.ParameterSelector.Length);
                parameter["total"] = cd.get_Parameter(ChannelData.ParameterSelector.Total);
                parameter["maximum"] = cd.get_Parameter(ChannelData.ParameterSelector.Maximum);
                parameter["average"] = cd.get_Parameter(ChannelData.ParameterSelector.Average);
                parameter["inertia"] = cd.get_Parameter(ChannelData.ParameterSelector.Inertia);
                parameter["centreOfGravity"] = cd.get_Parameter(ChannelData.ParameterSelector.CentreOfGravity);
                parameter["fillFactor"] = cd.get_Parameter(ChannelData.ParameterSelector.FillFactor);
                parameter["asymmetry"] = cd.get_Parameter(ChannelData.ParameterSelector.Asymmetry);
                parameter["numberOfCells"] = cd.get_Parameter(ChannelData.ParameterSelector.NumberOfCells);
                parameter["sampleLength"] = cd.get_Parameter(ChannelData.ParameterSelector.SampleLength);
                parameter["timeOfArrival"] = cd.get_Parameter(ChannelData.ParameterSelector.TimeOfArrival);
                parameter["first"] = cd.get_Parameter(ChannelData.ParameterSelector.First);
                parameter["last"] = cd.get_Parameter(ChannelData.ParameterSelector.Last);
                parameter["minimum"] = cd.get_Parameter(ChannelData.ParameterSelector.Minimum);
                parameter["swscov"] = cd.get_Parameter(ChannelData.ParameterSelector.SWSCOV);
                parameter["variableLength"] = cd.get_Parameter(ChannelData.ParameterSelector.VariableLength);

                parameters.Add(parameter);
            }

            particleData["parameters"] = parameters;

            return particleData;
        }

        private static List<Dictionary<string, object>> LoadChannels(DataFileWrapper dfw)
        {
            var channels = new List<Dictionary<string, object>>();

            foreach (var channelList in dfw.CytoSettings.ChannelList)
            {
                var channel = new Dictionary<string, object>();

                channel["id"] = channelList.ID;
                channel["description"] = channelList.Description;
                channel["colour"] = channelList.DefaultColor;
                channels.Add(channel);
            }

            return channels;
        }

        private static Dictionary<string, object> LoadMeasurementResults(DataFileWrapper dfw)
        {
            var measurementResults = new Dictionary<string, object>();

            measurementResults["start"] = dfw.MeasurementInfo.MeasurementStart;

            measurementResults["duration"] = dfw.MeasurementInfo.ActualMeasureTime;
            // measurementResults["maximum_measurement_time_s"] = dfw.MeasurementInfo.ActualMeasureTime; // VELIZ line
            measurementResults["particleCount"] = dfw.MeasurementInfo.NumberofCountedParticles;
            measurementResults["particlesInFileCount"] = dfw.MeasurementInfo.NumberofSavedParticles;
            measurementResults["pictureCount"] = dfw.MeasurementInfo.NumberOfPictures;
            measurementResults["pumpedVolume"] = dfw.pumpedVolume; // muL
            measurementResults["analysedVolume"] = dfw.analyzedVolume; // muL
            measurementResults["particleConcentration"] = dfw.Concentration; // n/muL

            // Auxiliary Sensor Data
            measurementResults["systemTemperature"] = dfw.MeasurementInfo.SystemTemp; // C
            measurementResults["sheathTemperature"] = dfw.MeasurementInfo.SheathTemp; // C
            measurementResults["pressureAbsolute"] = dfw.MeasurementInfo.ABSPressure; // mbar
            measurementResults["pressureDifferential"] = dfw.MeasurementInfo.DiffPressure; // mbar
            measurementResults["PMTtemperature"] = dfw.MeasurementInfo.PMTTemp; //C
            measurementResults["buoyTemperature"] = dfw.MeasurementInfo.BuoyTemp; //C
            measurementResults["referenceVoltageRatio"] = dfw.MeasurementInfo.VRefFactor;
            measurementResults["intVoltage"] = dfw.MeasurementInfo.intVoltage; //V
            measurementResults["rechargeCurrent"] = dfw.MeasurementInfo.internalRecharge; //mA
            measurementResults["laserTemperature"] = dfw.MeasurementInfo.LaserTemp; // C

            // The following lines are not necessary but can be uncommented to export the corresponding data

            // Logs
            // measurementResults["systemTemperatureLogs"] = dfw.MeasurementInfo.sensorLogs.SystemTemp; // C
            // measurementResults["sheathTemperatureLogs"] = dfw.MeasurementInfo.sensorLogs.SheathTemp; // C
            // measurementResults["PMTtemperatureLogs"] = dfw.MeasurementInfo.sensorLogs.PMTTemp; //C
            // measurementResults["buoyTemperatureLogs"] = dfw.MeasurementInfo.sensorLogs.BuoyTemp; //C
            // measurementResults["referenceVoltageRatioLogs"] = dfw.MeasurementInfo.sensorLogs.VRefFactor;
            // measurementResults["extSupplyPowerVoltageLogs"] = dfw.MeasurementInfo.sensorLogs.extSupplyPowerVoltage; //V
            // measurementResults["buoyVoltageLogs"] = dfw.MeasurementInfo.sensorLogs.BuoyExtBatteryVoltage; //V
            // measurementResults["pressureExtLogs"] = dfw.MeasurementInfo.sensorLogs.extPressure; //bar
            // measurementResults["filterPressureLogs"] = dfw.MeasurementInfo.sensorLogs.ExternalFiltersPressureData; //mbar
            // measurementResults["intVoltageLogs"] = dfw.MeasurementInfo.sensorLogs.intVoltage; //V
            // measurementResults["rechargeCurrentLogs"] = dfw.MeasurementInfo.sensorLogs.internalRecharge; //mA
            // measurementResults["laserTemperatureLogs"] = dfw.MeasurementInfo.sensorLogs.LaserTemp; //C

            // Laser data
            // measurementResults["laser1BaseTemperature"] = dfw.MeasurementInfo.Laser1BaseTemperature; //C
            // measurementResults["laser1BaseTemperatureLogs"] = dfw.MeasurementInfo.sensorLogs.Laser1BaseTemperature; //C
            // measurementResults["laser1DiodeTemperature"] = dfw.MeasurementInfo.Laser1DiodeTemperature; //C
            // measurementResults["laser1DiodeTemperatureLogs"] = dfw.MeasurementInfo.sensorLogs.Laser1DiodeTemperature; //C
            // measurementResults["laser1DiodeCurrent"] = dfw.MeasurementInfo.Laser1DiodeCurrent; //mA
            // measurementResults["laser1DiodeCurrentLogs"] = dfw.MeasurementInfo.sensorLogs.Laser1DiodeCurrent; //mA
            // measurementResults["laser1TECLoad"] = dfw.MeasurementInfo.Laser1TecLoad; //%
            // measurementResults["laser1TECLoadLogs"] = dfw.MeasurementInfo.sensorLogs.Laser1TecLoad; //%
            // measurementResults["laser1InputVoltage"] = dfw.MeasurementInfo.Laser1InputVoltage; //V
            // measurementResults["laser1InputVoltageLogsc"] = dfw.MeasurementInfo.sensorLogs.Laser1InputVoltage; //V
            // measurementResults["laser1Mode"] = dfw.MeasurementInfo.Laser1Mode;

            return measurementResults;
        }


        /// <summary>
        /// Very simple strtucutre we use to represent a cropping rectangle during the JSON generation.
        /// The x,y, cordinates are the location of the top left, adn width and height indicate the size.
        /// </summary>
        /// <param name="X">X value of TOP left.</param>
        /// <param name="Y">Y value of TOP left.</param>
        /// <param name="Width">Width of the cropped image.</param>
        /// <param name="Height">Height of the cropped image.</param>
        private record struct CroppingRectangle(int X, int Y, int Width, int Height);

        /// <summary>
        /// Convert a n open CV rectangle to our own structure, with only the 4 values we want, this makes the JSON that is output a bit
        /// shorter, nicer, and easier to understand.
        /// </summary>
        /// <param name="rect"></param>
        /// <returns>The CroppingRectangle equivalent of the OpenCV rect.</returns>
        private static CroppingRectangle ToCroppingRectangle(OpenCvSharp.Rect rect)
        {
            return new CroppingRectangle { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height };
        }

        /// <summary>
        /// Get the cropping rectangle for the image.  If the image is not cropped then we will simply return the
        /// entire size of the image as a rectangle.
        /// </summary>
        /// <param name="img"></param>
        /// <returns></returns>
        private static CroppingRectangle GetCroppingRectangle(CytoImage img)
        {
            if (img.IsCropped) {
                return ToCroppingRectangle(img.CropRect);
            } else  {
                var imgMat = img.ImageMat;
                return new CroppingRectangle { X = 0, Y = 0, Width = imgMat.Width, Height = imgMat.Height };
            }
        }

        /// <summary>
        /// Load the image into memory in a format (base64) that can be exported to JSON easily.  Depending on the
        /// image processing options this can be the complete unprocessed image, or we can do object detection
        /// and cropping on the fly during the export process.
        /// </summary>
        /// <param name="dfw"></param>
        /// <param name="imgOpts"></param>
        /// <returns></returns>
        private static List<Dictionary<string, object>> LoadImages(DataFileWrapper dfw, ImageProcessingOptions imgOpts)
        {
            var images = new List<Dictionary<string, object>>();

            int[] param = [ (int)ImwriteFlags.JpegQuality, 95 ];

            foreach (var particle in dfw.SplittedParticlesWithImages)
            {
                var image = new Dictionary<string, object>();

                if (particle.ImageHandling?.ImageStream?.Length > 0)
                {
                    image["particleId"] = particle.ID;

                    if (imgOpts.Process) {
                        var img_rect = particle.ImageHandling.GetCroppedImageWithRect(ToImgProcSettings(imgOpts));
                        Cv2.ImEncode(".jpeg", img_rect.Item1, out byte[]? buff, param);
                        image["base64"]        =  System.Convert.ToBase64String(buff!);
                        image["cropRectangle"] = ToCroppingRectangle(img_rect.Item2);
                    }
                    else {
                        particle.ImageHandling.ImageStream.Position = 0;
                        using (MemoryStream memoryStream = new MemoryStream()) {
                            particle.ImageHandling?.ImageStream?.CopyTo(memoryStream);
                            image["base64"] = System.Convert.ToBase64String(memoryStream.ToArray());
                        }
                        image["cropRectangle"] = GetCroppingRectangle(particle.ImageHandling!);
                    }
                    images.Add(image);
                }
            }
            return images;
        }

        public class IgnoreErrorPropertiesResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                property.ShouldSerialize = instance =>
                {
                    try
                    {
                        var value = property!.ValueProvider!.GetValue(instance);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                };
                return property;
            }
        }
    }
}
			