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
            
            // Dictionaries to store interfaces for site cohort data to provide O(1) access instead of native O(n)
            // This state only survives the current timestep and most of these dictionaries are cleared after every site iteration.
            Dictionary<ISpecies, ISpeciesCohorts> speciesCohortsLookup = new Dictionary<ISpecies, ISpeciesCohorts>();
            //probably unnecessary, but I will leave it for now
            Dictionary<ISpecies, Dictionary<ushort, ICohort>> ageLookup = new Dictionary<ISpecies, Dictionary<ushort, ICohort>>();
            Dictionary<ISpecies, SpeciesCohorts> concreteSpeciesCohortsLookup = new Dictionary<ISpecies, SpeciesCohorts>();
            Dictionary<ISpecies, Dictionary<ushort, int>> biomassTransfer = new Dictionary<ISpecies, Dictionary<ushort, int>>();
            
            // Define biomass transfer rules (source species -> target species)
            //TODO: Extract transfer rules from user provided species order (or maybe even from the species matrix)
            var biomassTransferRules = new Dictionary<string, string>
            {
                { "FAGU.GRA", "FAGU.GR1" },
                { "FAGU.GR1", "FAGU.GR2" },
                { "FAGU.GR2", "FAGU.GR3" },
            };
            
            // Create species name to ISpecies mapping dictionary
            var speciesNameToISpecies = new Dictionary<string, ISpecies>();
            foreach (var species in PlugIn.ModelCore.Species) {
                speciesNameToISpecies[species.Name] = species;
            }
            
            //TODO: Add user specified transition matrix
            
            foreach (ActiveSite site in sites) {
                SiteCohorts siteCohorts = SiteVars.Cohorts[site];
                // Stage 1: Build dictionaries for this site
                foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                    speciesCohortsLookup[speciesCohorts.Species] = speciesCohorts;
                    
                    // Store concrete type for AddNewCohort access
                    concreteSpeciesCohortsLookup[speciesCohorts.Species] = (SpeciesCohorts)speciesCohorts;
                    
                    // Build age lookup for O(1) age access within each species
                    var speciesAgeLookup = new Dictionary<ushort, ICohort>();
                    foreach (ICohort cohort in speciesCohorts) {
                        speciesAgeLookup[cohort.Data.Age] = cohort;
                    }
                    ageLookup[speciesCohorts.Species] = speciesAgeLookup;
                }
                
                // Debug output for specific site
                //if (site.Location.Row == 136 && site.Location.Column == 1) {
                    foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                        string speciesName = speciesCohorts.Species.Name;
                        foreach (ICohort cohort in speciesCohorts) {
                            if (speciesName == "FAGU.GRA" || speciesName == "FAGU.GR1" || speciesName == "FAGU.GR2" || speciesName == "FAGU.GR3") {
                                PlugIn.ModelCore.UI.WriteLine($"Site: ({site.Location.Row},{site.Location.Column}), Species: {speciesName}, Age: {cohort.Data.Age}, Biomass: {cohort.Data.Biomass}");
                            }
                        }
                    }
                //}
                
                // Stage 2: Biomass transfer logic using O(1) dictionary lookups
                
                // Look through ALL cohorts and check for specific species
                foreach (ISpeciesCohorts speciesCohorts in siteCohorts) {
                    string speciesName = speciesCohorts.Species.Name;
                    foreach (ICohort cohort in speciesCohorts) {
                        if (biomassTransferRules.TryGetValue(speciesName, out string targetSpeciesName)) {
                            PlugIn.ModelCore.UI.WriteLine($"1Transferring {speciesName} to {targetSpeciesName}");

                            int transfer = (int)(cohort.Data.Biomass * 0.3);
                            cohort.ChangeBiomass(-transfer);
                            ISpecies targetSpecies = speciesNameToISpecies[targetSpeciesName];
                            if (!biomassTransfer.ContainsKey(targetSpecies)) {
                                biomassTransfer[targetSpecies] = new Dictionary<ushort, int>();
                            }
                            biomassTransfer[targetSpecies][cohort.Data.Age] = transfer;
                            PlugIn.ModelCore.UI.WriteLine($"2: {targetSpeciesName} has {biomassTransfer[targetSpecies][cohort.Data.Age]}");
                        }
                    }
                }
                
                // Process transfers to target species using O(1) lookups
                foreach (KeyValuePair<ISpecies, Dictionary<ushort, int>> speciesEntry in biomassTransfer) {
                    ISpecies targetSpecies = speciesEntry.Key;
                    
                    // O(1) species access
                    if (speciesCohortsLookup.TryGetValue(targetSpecies, out var targetCohorts)) {
                        foreach (KeyValuePair<ushort, int> ageEntry in speciesEntry.Value) {
                            ushort age = ageEntry.Key;
                            int transfer = ageEntry.Value;
                            
                            // O(1) age lookup
                            //if (targetSpecies.Name == "FAGU.GR1") {
                                PlugIn.ModelCore.UI.WriteLine($"Transferring {transfer} biomass to {targetSpecies.Name}");
                            //}
                            if (ageLookup[targetSpecies].TryGetValue(age, out var targetCohort)) {
                                targetCohort.ChangeBiomass(transfer);
                            } else {
                                // Use concrete type for O(1) AddNewCohort access
                                if (concreteSpeciesCohortsLookup.TryGetValue(targetSpecies, out var concreteCohorts)) {
                                    concreteCohorts.AddNewCohort(age, transfer, new ExpandoObject());
                                } else {
                                    // Fallback to site-level O(n) AddNewCohort if species doesn't exist yet
                                    siteCohorts.AddNewCohort(targetSpecies, age, transfer, new ExpandoObject());
                                }
                            }
                        }
                    }
                }
                
                // Clear inner dictionaries for reuse
                // It's probably redundant clearing the inner dictionaries
                foreach (var innerDict in biomassTransfer.Values) {
                    innerDict.Clear();
                }
                biomassTransfer.Clear();
                
                // Clear dictionaries for reuse at end of iteration
                speciesCohortsLookup.Clear();
                ageLookup.Clear();
                concreteSpeciesCohortsLookup.Clear();
            }
        }
    }
} 