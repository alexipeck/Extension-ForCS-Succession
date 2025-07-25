using Landis.Core;
using Landis.SpatialModeling;
using Landis.Library.UniversalCohorts;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;

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
                    SpeciesCohorts concreteSpeciesCohorts = (SpeciesCohorts)speciesCohorts;
                    foreach (ICohort cohort in concreteSpeciesCohorts) {
                        if (biomassTransferRules.TryGetValue(concreteSpeciesCohorts.Species.Name, out string targetSpeciesName)) {
                            int transfer = (int)(cohort.Data.Biomass * 0.99);
                            cohort.ChangeBiomass(-transfer);
                            ISpecies targetSpecies = speciesNameToISpecies[targetSpeciesName];
                            if (!biomassTransfer.ContainsKey(targetSpecies)) {
                                biomassTransfer[targetSpecies] = new Dictionary<ushort, int>();
                            }
                            biomassTransfer[targetSpecies][cohort.Data.Age] = transfer;
                            //PlugIn.ModelCore.UI.WriteLine($"Transferring from {speciesCohorts.Species.Name} to {targetSpeciesName}: {transfer}");
                        }
                    }
                }

                /* foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                    if (biomassTransfer.TryGetValue(speciesCohorts.Species, out Dictionary<ushort, int> speciesBiomassTransfer)) {
                        SpeciesCohorts concreteSpeciesCohorts = (SpeciesCohorts)speciesCohorts;
                        foreach (ICohort cohort in concreteSpeciesCohorts) {
                            if (speciesBiomassTransfer.TryGetValue(cohort.Data.Age, out int transfer)) {
                                cohort.ChangeBiomass(transfer);
                                speciesBiomassTransfer.Remove(cohort.Data.Age);
                            }
                        }
                        foreach (KeyValuePair<ushort, int> remainingTransfer in speciesBiomassTransfer) {
                            ushort age = remainingTransfer.Key;
                            int transfer = remainingTransfer.Value;
                            siteCohorts.AddNewCohort(speciesCohorts.Species, age, transfer, new ExpandoObject());
                        }
                        biomassTransfer.Remove(speciesCohorts.Species);
                    }
                } */
                biomassTransfer.Clear();
            }
        }
    }
} 
