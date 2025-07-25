using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.UniversalCohorts;
using System.Collections.Generic;
using System.Dynamic;

namespace Landis.Extension.Succession.ForC
{
    public static class BBDHandler
    {


        public static void ProcessBBD()
        {
            IEnumerable<ActiveSite> sites = /* ModelCore.Landscape */PlugIn.ModelCore.Landscape.ActiveSites;
            
            Dictionary<ISpecies, Dictionary<ushort, int>> biomassTransfer = new Dictionary<ISpecies, Dictionary<ushort, int>>();
            
            // Define biomass transfer rules (source species -> target species)
            //TODO: Extract transfer rules from user provided species order (or maybe even from the species matrix)
            var biomassTransferRules = new Dictionary<string, string>
            {
                { "FAGU.GRA", "FAGU.GR1" },
                { "FAGU.GR1", "FAGU.GR2" },
                { "FAGU.GR2", "FAGU.GR3" },
            };
            
            Dictionary<string, ISpecies> speciesNameToISpecies = new Dictionary<string, ISpecies>();
            foreach (var species in PlugIn.ModelCore.Species) {
                speciesNameToISpecies[species.Name] = species;
            }
            
            //TODO: Add user specified transition matrix
            
            foreach (ActiveSite site in sites) {
                SiteCohorts siteCohorts = SiteVars.Cohorts[site];
                
                // Debug output for specific site
                if (site.Location.Row == 136 && site.Location.Column == 1) {
                    foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                        string speciesName = speciesCohorts.Species.Name;
                        foreach (ICohort cohort in speciesCohorts) {
                            if (speciesName == "FAGU.GRA" || speciesName == "FAGU.GR1" || speciesName == "FAGU.GR2" || speciesName == "FAGU.GR3") {
                                PlugIn.ModelCore.UI.WriteLine($"Site: ({site.Location.Row},{site.Location.Column}), Species: {speciesName}, Age: {cohort.Data.Age}, Biomass: {cohort.Data.Biomass}");
                            }
                        }
                    }
                }
                
                foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                    foreach (ICohort cohort in speciesCohorts) {
                        if (biomassTransferRules.TryGetValue(speciesCohorts.Species.Name, out string targetSpeciesName)) {
                            int transfer = (int)(cohort.Data.Biomass * 0.3);
                            cohort.ChangeBiomass(-transfer);
                            ISpecies targetSpecies = speciesNameToISpecies[targetSpeciesName];
                            if (!biomassTransfer.ContainsKey(targetSpecies)) {
                                biomassTransfer[targetSpecies] = new Dictionary<ushort, int>();
                            }
                            biomassTransfer[targetSpecies][cohort.Data.Age] = transfer;
                        }
                    }
                }
                
                foreach (KeyValuePair<ISpecies, Dictionary<ushort, int>> speciesEntry in biomassTransfer) {
                    ISpecies targetSpecies = speciesEntry.Key;
                    
                    foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                        if (speciesCohorts.Species == targetSpecies) {
                            foreach (ICohort cohort in speciesCohorts) {
                                ushort age = cohort.Data.Age;
                                if (speciesEntry.Value.TryGetValue(age, out int transfer)) {
                                    cohort.ChangeBiomass(transfer);
                                    speciesEntry.Value.Remove(age);
                                }
                            }
                            break;
                        }
                    }
                    
                    // Create new cohorts for any remaining transfers
                    foreach (KeyValuePair<ushort, int> remainingTransfer in speciesEntry.Value) {
                        ushort age = remainingTransfer.Key;
                        int transfer = remainingTransfer.Value;
                        siteCohorts.AddNewCohort(targetSpecies, age, transfer, new ExpandoObject());
                    }
                }
                
                // Clear transfer dictionary for reuse
                biomassTransfer.Clear();
            }
        }
    }
} 