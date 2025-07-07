using CytoSense.Data;
using CytoSense.Data.Analysis;
using CytoSense.Serializing;
using System.Xml;
using CytoSense.MeasurementSettings;
using System.Runtime;
using System.Collections.Generic;
using System.Diagnostics;

namespace Cyz2Json
{
    /// <summary>
    /// This class was added to dump region information and statistics for the regions that were used for imaging or
    /// for a specific region definition override that was specified on the command line.
    /// </summary>
    /// <remarks>I decided to put it into a separate file, to keep my changes apart from other work, and
    /// to keep the main program manageable. </remarks>
    internal class RegionInformation
    {
        internal record class RegionStatistics(string name="", int count=0, int images=0);


        public string definition;
        public List<RegionStatistics> statistics;

        /// <summary>
        /// Load set definitions from either specified file, or from the datafile wrapper, and calculate
        /// all the required information that needs to be put into the JSON file.  This is like a constructor
        /// but I put it in a separate function to make it clear how much work it is to calculate all this.
        /// </summary>
        /// <param name="dfw"></param>
        /// <param name="regionDefinitionFile"></param>
        /// <returns></returns>
        public static RegionInformation LoadRegionInformation( DataFileWrapper dfw, FileInfo regionDefinitionFile)
        {
            SetsList sets = (regionDefinitionFile != null)?LoadSetsFromFile(dfw, regionDefinitionFile):LoadSetsFromDfw(dfw);
            
            sets = sets.Clone(dfw); // Get a copy of the sets that are applied to our specific datafile. We do not need to old one, so we reuse the variable.


            RecalculateParticleIndices(dfw, sets); // NOTE: Verify this is need after loading a new file?

            var stats = new List<RegionStatistics>();
            foreach( var set in sets) {
                var name = set.Name;
                var count = set.ParticleIndices.Length; 
                int images = set.Particles.Where( (p)=> p.hasImage).Count();
                stats.Add( new RegionStatistics(name, count, images) );
            }


            return new RegionInformation(){ 
                definition = XmlSerialize(sets, dfw.CytoSettings.machineConfigurationRelease.ReleaseDate),
                statistics = stats
            };
        }

        /// <summary>
        /// Process all particles and sets to findout where they belong.  The exclusive sets mode is a bit tricky,
        /// a particle can only belong to one set, even it would match other sets as well. (Only for GateBases sets).
        /// To do this we must process the sets starting from the top, and once a particle has been assigned it is no
        /// longer available for other sets.
        /// </summary>
        /// <param name="dfw"></param>
        /// <param name="sets"></param>
        private static void RecalculateParticleIndices(DataFileWrapper dfw, SetsList sets)
        {
            //  update gatebased sets without taking into account exclusive set mode
            foreach(var set in sets) {
                if (set.type == cytoSetType.gateBased) {
                    set.RecalculateParticleIndices();
                }
            }

            if ( sets.ExclusiveSets) {
                // in exclusive set mode, filter out particles used by higher ranked sets
                var usedParticleIndicesArrayList = new List<int[]>();
                foreach(var set in sets) {
                    if (set is gateBasedSet gbSet)
                    {
                        gbSet.CalculateExclusiveParticleIndices(usedParticleIndicesArrayList);
                        usedParticleIndicesArrayList.Add(gbSet.ParticleIndices);
                    } // Else not a gatebased set, so we do not need to process it.            
                }
            }

            // update combined sets, OrSets and unassigned particles set
            foreach(var set in sets) {
                switch(set.type) {
                    case cytoSetType.combined:
                        set.RecalculateParticleIndices();
                        break;
                    case cytoSetType.OrSet:
                        set.RecalculateParticleIndices();
                        break;
                    case cytoSetType.unassignedParticles:
                        set.RecalculateParticleIndices();
                        break;
                }
            }
        }

        private static SetsList LoadSetsFromFile(DataFileWrapper dfw, FileInfo regionDefinitionFile)
        {
            if (regionDefinitionFile.Extension == ".iif") {

                var iifParams = Serializing.loadFromFile<IIFParameters>(regionDefinitionFile.FullName);
                return ConvertIifParametersToSetsList(dfw, iifParams);
            } else if (regionDefinitionFile.Extension == ".xml") {
                return SetsList.XmlDeSerialize(dfw.CytoSettings, regionDefinitionFile.FullName);
            } else {
                throw new Exception(String.Format("Region definition file extensions '{0}' is not supported, valid extensions are '.xml' and '.iif'", regionDefinitionFile.Extension ));
            }
        }

        private static SetsList LoadSetsFromDfw( DataFileWrapper dfw)
        {
            var mSettings = dfw.MeasurementSettings;
            if (mSettings.IIFuseTargetRange) {
                return ConvertIifParametersToSetsList(dfw, mSettings.IIFParameters);
            } else if( mSettings.IIFUseSetDefintionSelector) {
                // In CytoClus we filter all unused sets when importing them into our workspace. But in this case simply importing all sets
                // will not be a problem.
                return SetsList.XmlDeSerializeString(dfw.CytoSettings, mSettings.IIFSetDefintionXml);
            }
            else {
                throw new Exception("No region information is provided in the file, please provide a region definition file if you want to export region information");
            }
        }

        private static SetsList ConvertIifParametersToSetsList(DataFileWrapper dfw, IIFParameters iifPars)
        {
            if (iifPars.xml != null ) { 
                throw new Exception("Old CytoClus 3 target range definitions are not supported by this tool");
            }
            var sets = new SetsList(dfw.CytoSettings.SerialNumber);
            var iifSet = new gateBasedSet( (gateBasedSet) iifPars.cc4set ); // ' Create a copy to add to the set!
            sets.Add(iifSet);
            return sets;
        }

        private static string XmlSerialize(SetsList sets, DateTime configDate)
        {
            var xmlDocument = new XmlDocument();
            var rootElement = xmlDocument.CreateElement("SetList");

            xmlDocument.AppendChild(rootElement);
            AddAttribute(xmlDocument,rootElement, "configuration_date", configDate.ToString("o", new System.Globalization.NumberFormatInfo()));

            sets.XmlDocumentWrite(xmlDocument, rootElement);

            return xmlDocument.OuterXml;
        }

        private static XmlAttribute AddAttribute(XmlDocument doc, XmlElement node, string name, string value)
        {
            var attribute = doc.CreateAttribute(name);
            attribute.Value = value;
            node.SetAttributeNode(attribute);
            return attribute;
        }


        private RegionInformation()
        {
            definition = "";
        }

    }
}
