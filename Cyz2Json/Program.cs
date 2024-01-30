//
// Cyz2Json
//
// Convert CYZ flow cytometry files to a JSON format.
//
// Copyright(c) 2023 Centre for Environment, Fisheries and Aquaculture Science.
//

using CytoSense.Data;
using CytoSense.CytoSettings;
using CytoSense.Data.ParticleHandling;
using CytoSense.Data.ParticleHandling.Channel;
using Newtonsoft.Json;
using System.CommandLine;

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

namespace Cyz2Json
{
    internal class Program
    {

        static void Main(string[] args)
        {
            var inputArgument = new Argument<FileInfo>(
                "input",
            description: "CYZ input file");

            var outputOption = new Option<FileInfo>(
                "--output",
                description: "JSON output file");

            var rawOption = new Option<bool>(
                name: "--raw",
                description: "Do not apply the moving weighted average filtering algorithm to pulse shapes. Export raw, unsmoothed data.");

            var rootCommand = new RootCommand("Convert CYZ files to JSON")
            {
                inputArgument, outputOption, rawOption
            };

            rootCommand.SetHandler(Convert, inputArgument, outputOption, rawOption);

            rootCommand.Invoke(args);
        }
        /// <summary>
        /// Convert the flow cytometry data in the file designated by cyzFilename to Javscript Object Notation (JSON).
        /// If jsonFilename is null, echo the JSON to the console, otherwise store it a file designated by jsonFilename.
        /// </summary>
        static void Convert(FileInfo cyzFilename, FileInfo jsonFilename, bool isRaw)
        {
            var data = LoadData(cyzFilename.FullName, isRaw);

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
        private static Dictionary<string, object> LoadData(string pathname, bool isRaw)
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
            data["instrument"] = LoadInstrument(dfw);
            data["particles"] = LoadParticles(dfw, isRaw);
            data["images"] = LoadImages(dfw);

            return data;
        }

        private static List<Dictionary<string, object>> LoadParticles(DataFileWrapper dfw, bool isRaw)
        {
            var particles = new List<Dictionary<string, object>>();

            foreach (var particle in dfw.SplittedParticles)
                particles.Add(LoadParticle(particle, isRaw));

            return particles;
        }

        private static Dictionary<string, object> LoadParticle(Particle particle, bool isRaw)
        {

            var particleData = new Dictionary<string, object>();

            particleData["particleId"] = particle.ID;
            particleData["hasImage"] = particle.hasImage;

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

        private static Dictionary<string, object> LoadMeasurementSettings(DataFileWrapper dfw)
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

            return measurementSettings;
        }

        private static Dictionary<string, object> LoadMeasurementResults(DataFileWrapper dfw)
        {
            var measurementResults = new Dictionary<string, object>();

            measurementResults["start"] = dfw.MeasurementInfo.MeasurementStart;

            measurementResults["duration"] = dfw.MeasurementInfo.ActualMeasureTime;
            measurementResults["particleCount"] = dfw.MeasurementInfo.NumberofCountedParticles;
            measurementResults["particlesInFileCount"] = dfw.MeasurementInfo.NumberofSavedParticles;
            measurementResults["pictureCount"] = dfw.MeasurementInfo.NumberOfPictures;
            measurementResults["pumpedVolume"] = dfw.pumpedVolume; // muL
            measurementResults["analysedVolume"] = dfw.analyzedVolume; // muL
            measurementResults["particleConcentration"] = dfw.Concentration; // n/muL

            // Auxiliary Sensor Data

            measurementResults["systemTemperature"] = dfw.MeasurementInfo.SystemTemp; // C
            measurementResults["sheathTemperature"] = dfw.MeasurementInfo.SheathTemp; // C
            measurementResults["absolutePressure"] = dfw.MeasurementInfo.ABSPressure; // mbar
            measurementResults["differentialPressure"] = dfw.MeasurementInfo.DiffPressure; // mbar

            return measurementResults;
        }

        private static List<Dictionary<string, object>> LoadChannels(DataFileWrapper dfw)
        {
            var channels = new List<Dictionary<string, object>>();

            foreach (var channelList in dfw.CytoSettings.ChannelList)
            {
                var channel = new Dictionary<string, object>();

                channel["id"] = channelList.ID;
                channel["description"] = channelList.Description;
                channels.Add(channel);
            }

            return channels;
        }

        private static Dictionary<string, object> LoadInstrument(DataFileWrapper dfw)
        {
            var instrument = new Dictionary<string, object>();

            instrument["name"] = dfw.CytoSettings.name;
            instrument["serialNumber"] = dfw.CytoSettings.SerialNumber;
            instrument["sampleCoreSpeed"] = dfw.CytoSettings.SampleCorespeed;
            instrument["laserBeamWidth"] = dfw.CytoSettings.LaserBeamWidth;
            instrument["channels"] = LoadChannels(dfw);
            instrument["measurementSettings"] = LoadMeasurementSettings(dfw);
            instrument["measurementResults"] = LoadMeasurementResults(dfw);

            return instrument;
        }

        private static List<Dictionary<string, object>> LoadImages(DataFileWrapper dfw)
        {
            var images = new List<Dictionary<string, object>>();

            foreach (var particle in dfw.SplittedParticlesWithImages)
            {
                var image = new Dictionary<string, object>();

                string base64String;

                using (MemoryStream memoryStream = new MemoryStream())
                {
                    particle.ImageHandling.ImageStream.Position = 0;
                    particle.ImageHandling.ImageStream.CopyTo(memoryStream);
                    
                    base64String = System.Convert.ToBase64String(memoryStream.ToArray());

                }
                image["particleId"] = particle.ID;
                image["base64"] = base64String;

                images.Add(image);
            }
            return images;
        }
    }
}