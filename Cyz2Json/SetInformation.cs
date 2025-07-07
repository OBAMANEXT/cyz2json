using CytoSense.Data;
using CytoSense.Data.ParticleHandling;
using CytoSense.Data.Analysis;
using CytoSense.Serializing;
using System.Xml;
using CytoSense.MeasurementSettings;

namespace Cyz2Json
{
    /// <summary>
    /// This class was added to export imaging set information and statistics for the sets that were used for imaging or
    /// for a specific set definition override that was specified on the command line.
    /// 
    /// The SetInformation collects all the data that we want to export, that is an XML description of the 
    /// sets, and some basic statistics per set.
    /// </summary>
    /// <remarks></remarks>
    internal class SetInformation
    {
        internal record class SetStatistics(string name="", int count=0, int images=0, double imaged_volume=0.0);


        public string definition;
        public List<SetStatistics> statistics;

        /// <summary>
        /// Load set definitions from either specified file, or from the datafile wrapper, and calculate
        /// all the required information that needs to be put into the JSON file.  This is like a constructor
        /// but I put it in a separate function to make it clear how much work it is to calculate all this.
        /// </summary>
        /// <param name="dfw">The datafile from which to load the information.</param>
        /// <param name="setDefinitionFile">An optional set definition file that overrides the definition stored in the datafile.</param>
        /// <returns>The set information, including set definition, and statistics, and the SetsList class with all
        /// the sets that have the information to determine which particles are in the set and which are not.  This can
        /// be used later when exporting per particle data.</returns>
        public static (SetInformation, SetsList) LoadImagingSetInformation( DataFileWrapper dfw, FileInfo ? setDefinitionFile)
        {
            if (!dfw.MeasurementSettings.IIFCheck) {
                throw new Exception("Imaging was not used in the datafile, so we cannot export imaging set information!");
            }

            SetsList imagingSets    = LoadSetsFromDfw(dfw);
            SetsList ? overrideSets = setDefinitionFile==null?null:LoadSetsFromFile(dfw, setDefinitionFile);
            SetsList sets = overrideSets==null?imagingSets:overrideSets;
            sets = sets.Clone(dfw); // Get a copy of the sets that are applied to our specific datafile. We do not need to old one, so we reuse the variable.

            RecalculateParticleIndices(dfw, sets); 

            if (overrideSets != null) { // We will need these sets to check if we can calculate a valid imaged volume.
                imagingSets = imagingSets.Clone(dfw);
                RecalculateParticleIndices(dfw, sets); 
            }

            var stats = new List<SetStatistics>();
            foreach( var set in sets) {
                var numParticles = set.ParticleIndices.Length;
                var numImagedParticles = set.Particles.Where( (p)=> p.hasImage).Count();
                var imagedVolume = Double.NaN;
                if (overrideSets==null) { 
                    imagedVolume =  CalculateImagedVolume_SetsFromDataFile(dfw,set,numParticles,numImagedParticles);
                } else {
                    imagedVolume =  CalculateImagedVolume_SetsFromOverride(dfw, imagingSets, set, numParticles,numImagedParticles);
                }
                // else, with a set override we do no know the volume, so we leave

                stats.Add( new SetStatistics( set.Name, numParticles, numImagedParticles,imagedVolume));
            }

            return (new SetInformation(){ 
                definition = XmlSerialize(sets, dfw.CytoSettings.machineConfigurationRelease.ReleaseDate),
                statistics = stats
            }, sets);
        }

        /// <summary>
        /// Calculate the imaged volume for the set. This is based on the number of particles in the set, and the number of imaged particles,
        /// combined with the analysed volume.
        /// 
        /// The actual type of set and the imaging settings in the file determine if it is actually valid to do this calculation. If it is not
        /// then the function will return Double.NaN instead of a number.
        /// 
        /// We can calculate a valid volume if this set was used for imaging, for any other sets we cannot.  There are 2 cases:
        /// 1) The target range option was used.
        ///    In this case there are 2 sets, one is the default set, for that one we cannot calculate a valid imaging volume.
        ///    The other set is the target set, so we can calculate a valid volume for that.
        ///    
        /// 2) Set Definition
        ///    This is the trickiest, we now need to check if the actual set was selected for imaging in the measurement
        ///    settings, or not.
        /// 
        /// 3) Target All
        ///   In this case, we targeted all, so we can calculate a valid ratio for the default set. (and any other set,
        ///   but there should not be any).
        /// 
        /// </summary>
        /// <param name="dfw">The datafile that is analysed.</param>
        /// <param name="set">The set to calculate the defintion for. </param>
        /// <param name="numParticlesInSet"></param>
        /// <param name="numImagedParticles"></param>
        /// <returns>The imaged volume for the set, if we can calculate it, or NaN if we cannot.</returns>
        private static double CalculateImagedVolume_SetsFromDataFile(DataFileWrapper dfw, CytoSet set, int numParticlesInSet, int numImagedParticles )
        {
            if (dfw.MeasurementSettings.IIFuseTargetRange) {
                if ( set.ListID != 0 ){
                    return ((double)numImagedParticles / numParticlesInSet) * dfw.analyzedVolume;
                } else { // Default set, no info.
                    return Double.NaN;
                }
            } else if (dfw.MeasurementSettings.IIFUseSetDefintionSelector) {
                if (dfw.MeasurementSettings.IIFSetSelectionInfo.Where( (ssi) => ssi.SetId == set.ListID && ssi.WantImages).Any()) {
                    return ((double)numImagedParticles / numParticlesInSet) * dfw.analyzedVolume;
                } else {
                    return Double.NaN;
                }
            } else if (dfw.MeasurementSettings.IIFuseTargetAll) {
                return ((double)numImagedParticles / numParticlesInSet) * dfw.analyzedVolume;
            } else {
                throw new Exception("Internal error: Invalid imaging settings while trying to calculate imaged volume.");
            }
        }
        
        /// <summary>
        /// Try to calculate the imaged volume for a set, if the set was loaded from an override definition file. This is a bit more tricky,
        /// this is only valid if the entire set (i.e. all recorded particles) are inside the targeted imaging range of the measurement.
        /// 
        /// Depending on the options used this can be easy to check, e.g. if the original measurement used target all, then all particles fall
        /// in that range.  For some other options we will have to check and compare the set set with the active imaging target sets for that file.
        /// 
        /// </summary>
        /// <param name="dfw"></param>
        /// <param name="imagingSets"></param>
        /// <param name="set"></param>
        /// <param name="numParticlesInSet"></param>
        /// <param name="numImagedParticles"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        /// <remarks>The current implementation fits within the rest, but it is not the most optimal.  The speed may improve if we move
        /// some work outside of this function so we can reuse it, or maybe restructure the code so we have a calculator object
        /// with member functions that can store intermediate results.</remarks>
        private static double CalculateImagedVolume_SetsFromOverride(DataFileWrapper dfw, SetsList imagingSets, CytoSet set, int numParticlesInSet, int numImagedParticles )
        {
            if (dfw.MeasurementSettings.IIFuseTargetRange) {
                if ( set.ListID != 0 ) {
                    // There will be a single target set in the imagingSets (and a default set at position 0). So we access that and check if
                    // if all particles are inside the original imaging target set.
                    var imgSet = imagingSets[1];
                    if( imgSet.ParticleIndices.Intersect( set.ParticleIndices ).Count() == set.ParticleIndices.Count() ) { 
                        return ((double)numImagedParticles / numParticlesInSet) * dfw.analyzedVolume;
                    } else { // Not all particles were in the target, so we cannot calculate a valid value.
                        return Double.NaN;
                    }
                } else { // Default set, no info.
                    return Double.NaN;
                }
            } else if (dfw.MeasurementSettings.IIFUseSetDefintionSelector) {
                // More complex we have to loop over all imaging sets (used) and check them to see if any actually match.
                if ( set.ListID != 0 ) { 
                    foreach (var imgSet in imagingSets) { 
                        if (dfw.MeasurementSettings.IIFSetSelectionInfo.Where( (ssi) => ssi.SetId == imgSet.ListID && ssi.WantImages).Any()) { 
                            if( imgSet.ParticleIndices.Intersect( set.ParticleIndices ).Count() == set.ParticleIndices.Count() ) { 
                                return ((double)numImagedParticles / numParticlesInSet) * dfw.analyzedVolume;
                            } // Not completely contained in this image set, so try again.
                        }
                    }
                }
                return Double.NaN;
            } else if (dfw.MeasurementSettings.IIFuseTargetAll) { 
                // All particles are (per definition) in the target range, so we can return a valid number.
                return ((double)numImagedParticles / numParticlesInSet) * dfw.analyzedVolume;
            } else {
                throw new Exception("Internal error: Invalid imaging settings while trying to calculate imaged volume.");
            }
        }


        /// <summary>
        /// Check which sets the particle is in, if any.  And return a list of set names
        /// that the particle is in.  This can be an empty list if the particle is not in any
        /// set. 
        /// 
        /// NOTE: If a null SetsList is passed, then there is no set definition then a null 
        /// list is returned instead of an empty list.
        /// </summary>
        /// <param name="p">The particle to check.</param>
        /// <param name="sets">The list of regions/sets to check, or null if there is nothing to check.</param>
        /// <returns>If a sets list is passed then a list with the names of the sets that the particle is in, which 
        /// can be empty. If the sets parameter is empty then a null will be returned to indicate that there is
        /// no set information</returns>
        /// <remarks>There always a default set with ListID 0, every particle is always in that.  Adding that
        /// to the result would only complicate things, so I ignore the default set when generating these set names.</remarks>
        public static List<string> ? LoadSetNames(Particle p, SetsList ? sets)
        {
            if (sets == null)
                return null;

            return sets.
                    Where( (s) => s.ListID != 0 && Array.BinarySearch(s.ParticleIndices,p.Index ) >= 0).
                    Select(s => s.Name).
                    ToList(); 
        }

        /// <summary>
        /// Process all particles and sets to find out where they belong.  The exclusive sets mode is a bit tricky,
        /// a particle can only belong to one set, even it would match other sets as well. (Only for GateBased sets).
        /// To do this we must process the sets starting from the top, and once a particle has been assigned it is no
        /// longer available for other sets.
        /// </summary>
        /// <param name="dfw">The data file with all the particles.</param>
        /// <param name="sets">The sets that should be recalculated.</param>
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
            if( dfw.MeasurementSettings.IIFuseSmartGrid ) {
                throw new Exception("Imaging set information exports are not valid  in combination with 'SmartGrid' imaging.");
            }

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
            } else if (mSettings.IIFuseTargetAll) {  // We target all images, so create a default set definition, with only the default set.
                return new SetsList(dfw.CytoSettings.SerialNumber);
            } else if( mSettings.IIFuseSmartGrid ) {
                throw new Exception("Imaging set information exports are not valid  in combination with 'SmartGrid' imaging.");
            } else {
                throw new Exception("Internal error, unhandled imaging option.");
            }
        }

        /// <summary>
        /// Convert the IIFParamaters information that was stored inside the measurement settings into
        /// a SetsList that can be used by the lest 
        /// </summary>
        /// <param name="dfw"></param>
        /// <param name="iifPars"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
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

        /// <summary>
        /// Utility function to serialize a SetsList to an XML string.
        /// </summary>
        /// <param name="sets">The sets to serialize.</param>
        /// <param name="configDate">The data of the instruments hardware configuration.</param>
        /// <returns>A string containing the XML definition of the SetsList.</returns>
        private static string XmlSerialize(SetsList sets, DateTime configDate)
        {
            var xmlDocument = new XmlDocument();
            var rootElement = xmlDocument.CreateElement("SetList");

            xmlDocument.AppendChild(rootElement);
            AddAttribute(xmlDocument,rootElement, "configuration_date", configDate.ToString("o", new System.Globalization.NumberFormatInfo()));

            sets.XmlDocumentWrite(xmlDocument, rootElement);

            return xmlDocument.OuterXml;
        }

        /// <summary>
        /// Utility function to make it easier to add an attribute to an XmlElement.
        /// </summary>
        /// <param name="doc">The outer document</param>
        /// <param name="node">The node to add the attribute to.</param>
        /// <param name="name">The name of the attribute.</param>
        /// <param name="value">The (string) value of the attribute.</param>
        /// <returns>The attribute that was added.</returns>
        private static XmlAttribute AddAttribute(XmlDocument doc, XmlElement node, string name, string value)
        {
            var attribute = doc.CreateAttribute(name);
            attribute.Value = value;
            node.SetAttributeNode(attribute);
            return attribute;
        }

        /// <summary>
        /// Default constructor, private so you cannot instantiate the class, only the public Load function can do so.
        /// </summary>
        private SetInformation()
        {
            definition = "";
            statistics = [];
        }

    }
}
