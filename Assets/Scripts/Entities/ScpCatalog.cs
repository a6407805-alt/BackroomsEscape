using System.Collections.Generic;
using UnityEngine;

namespace Backrooms
{
    /// <summary>
    /// Built-in 320+ SCP Foundation horror anomaly database (CC BY-SA 3.0, scpwiki.com).
    /// Merged with any StreamingAssets/Entities folders at startup; StreamingAssets folders with the same id override these
    /// (so SCP-049, SCP-096, SCP-106, SCP-939, and SCP-860-2 keep their full glTF models & ogg sound sets).
    /// EntityDirector deterministically drafts a rotating active subset each run & floor so players constantly face
    /// terrifying anomalies they haven't encountered before.
    /// </summary>
    public static class ScpCatalog
    {
        private readonly struct RawScp
        {
            public readonly string num;
            public readonly string title;
            public readonly string cls;
            public readonly string beh;
            public readonly string rig;
            public readonly string audio;
            public readonly float h;
            public readonly float walk;
            public readonly float run;
            public readonly byte flags; // 1=stealsVoices, 2=flickersLights, 4=freezesWhenWatched, 8=fearsFlashlight, 16=phaseWalls, 32=prefersDark
            public readonly string desc;

            public RawScp(string num, string title, string cls, string beh, string rig, string audio, float h, float walk, float run, byte flags, string desc)
            {
                this.num = num; this.title = title; this.cls = cls; this.beh = beh; this.rig = rig;
                this.audio = audio; this.h = h; this.walk = walk; this.run = run; this.flags = flags; this.desc = desc;
            }
        }

        // Flag helpers
        private const byte V = 1;   // stealsVoices (records & replays player mic!)
        private const byte L = 2;   // flickersLights (suppresses lights & drains battery)
        private const byte W = 4;   // freezesWhenWatched (moves at blinding speed when unobserved)
        private const byte F = 8;   // fearsFlashlight (stalls under direct spotlight beam)
        private const byte P = 16;  // phaseThroughWalls
        private const byte D = 32;  // prefersDark

        // 322 curated SCP horror anomalies (plus the 5 StreamingAssets glTF entities = 325+ unique SCPs)
        private static readonly RawScp[] Entries =
        {
            // --- Headline Nightmare Tier ---
            new("303", "The Doorman", "Euclid", "doorman", "doorman", "wheezing", 2.05f, 0.9f, 4.4f, L|D, "Formless reddish-brown humanoid that stands directly behind doorways and corridor turns, emitting rasping mechanical wheezes and inducing paralyzing dread."),
            new("783", "There Was A Crooked Man", "Keter", "ambusher", "crooked", "bone_snap", 2.65f, 1.3f, 6.2f, D, "Elongated, multi-jointed entity whose limbs snap and reset at impossible angles as it jerks down darkened halls."),
            new("610", "The Flesh That Hates", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.25f, 1.4f, 5.1f, 0, "Contagious, hyper-aggressive mutated biomass of fused limbs and gaping mouths seeking warm hosts."),
            new("4666", "The Yule Man", "Keter", "stalker", "skeletal_titan", "wheezing", 2.45f, 1.5f, 5.8f, V|L|P|D, "Gaunt, naked, smiling ancient humanoid that stalks isolated victims in total silence before striking in the dark."),
            new("4975", "Time's Up", "Euclid", "listener", "crooked", "clicking", 2.40f, 1.2f, 5.9f, D, "Avian-necked humanoid that rotates its cervical vertebrae with a rhythmic grandfather-clock CLICK-CLACK until the clicking suddenly stops right behind you."),
            new("3199", "Humans, Refuted", "Keter", "ambusher", "flesh_amalgam", "flesh_squelch", 2.15f, 1.6f, 6.4f, 0, "Featherless, grinning pale biped with an unhinged reproductive shriek that charges like a battering ram."),
            new("6448", "Not Deer", "Euclid", "mimic", "crooked", "bone_snap", 2.35f, 1.4f, 6.1f, V|D, "Wrong-jointed cervine doppelganger with forward-facing eyes that mimics distorted human cries from blind corners."),
            new("3333", "Tower", "Keter", "mimic", "doppelganger", "whisper_choir", 1.90f, 1.5f, 5.5f, V, "Hollowed skin-suit anomaly wearing a fellow explorer's silhouette and repeating your teammates' recorded voices."),
            new("280", "Eyes in the Dark", "Keter", "umbra", "shadow_wraith", "whisper_choir", 2.10f, 1.5f, 6.5f, F|D, "Pitch-black mass with two gleaming ocular points that glides silently in darkness and recoils from direct flashlight beams."),
            new("087-1", "The Stairwell Face", "Euclid", "doorman", "ocular_swarm", "sub_drone", 2.00f, 1.1f, 5.4f, L|D, "Floating, pupil-less pale face hovering just at the edge of light falloff in deep corridors."),
            new("3034", "The Counting Station", "Euclid", "mimic", "doppelganger", "radio_numbers", 1.85f, 1.2f, 5.6f, V|L, "Broadcast anomaly emitting descending Russian countdown tones and stolen human voice fragments; never answer its call."),
            new("2521", "●●|●●●●●|●●|●", "Keter", "shy", "shadow_wraith", "siren_static", 2.55f, 1.3f, 7.2f, L|P, "Tall ribbon-faced shadow entity drawn instantaneously to anyone who perceives or speaks of it."),
            new("3288", "The Aristocrats", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.10f, 1.6f, 5.8f, D, "Subterranean inbred cannibalistic predators with jawed torsos hunting in relentless packs."),
            new("076-2", "Able", "Keter", "hunter", "tall_lurker", "metallic_drag", 2.05f, 1.8f, 6.8f, 0, "Ancient tattooed warrior manifesting obsidian blades out of thin air and sprinting down any living target in sight."),
            new("966", "Sleep Killer", "Euclid", "cognito", "skeletal_titan", "wheezing", 1.95f, 1.3f, 4.9f, D, "Hairless, infrared-spectrum predator that drains sanity with infrasonic waves before moving in for the kill."),
            new("650", "Startling Statue", "Euclid", "weeping", "weeping_idol", "statue_scrape", 1.85f, 0.0f, 11.5f, W, "Smooth black mannequin that teleports silently right behind your back the instant nobody looks at it."),
            new("689", "Haunter in the Dark", "Keter", "weeping", "weeping_idol", "statue_scrape", 1.75f, 0.0f, 12.5f, W|L|D, "Soapstone skeletal idol that extinguishes lights and kills whoever breaks eye contact in the dark."),
            new("895", "Camera Disruption", "Euclid", "cognito", "doorman", "siren_static", 2.10f, 1.1f, 5.0f, L|D, "Oak coffin aura causing violent visual hallucinations, chromatic distortion, and power failure within 15 metres."),
            new("1471", "MalO ver1.0.0", "Euclid", "cognito", "tall_lurker", "whisper_choir", 2.15f, 1.4f, 5.3f, V|D, "Canid-skulled dark figure stalking peripheral vision and echoing distorted voice messages."),
            new("513-1", "A Cowbell", "Euclid", "cognito", "shadow_wraith", "metallic_drag", 2.20f, 1.5f, 5.7f, V|D, "Tall emaciated spectre triggered by metallic ringing that stalks exhausted minds relentlessly."),
            new("1983", "Doorway to Nowhere", "Keter", "umbra", "shadow_wraith", "sub_drone", 2.10f, 1.5f, 6.0f, L|F|P|D, "Light-absorbing heart-thief emerging from dead-end thresholds."),
            new("3001", "Red Reality", "Euclid", "stalker", "flesh_amalgam", "sub_drone", 2.05f, 1.2f, 4.8f, V|L|P, "Decaying non-space remnant with a single blinking red recording light whispering broken pleas."),
            new("2774", "The Slow Burn Sloth", "Keter", "cognito", "doppelganger", "siren_static", 1.90f, 1.1f, 5.0f, L, "Uncanny sloth-costumed figure whose brief visual flicker hijacks motor control and drains sanity."),
            new("3515", "Unearth", "Safe", "stalker", "crawler_quad", "bone_snap", 1.55f, 1.2f, 4.8f, L|P|D, "Suffocating claustrophobic subterranean lurker pulling victims beneath the floorboards."),
            new("4885", "Find Him", "Keter", "shy", "crooked", "bone_snap", 2.30f, 1.4f, 7.0f, P, "Pale striped contortionist that hunts anyone who learns its location, crawling out from impossible angles."),
            new("5000", "Why?", "Safe", "hunter", "doppelganger", "metallic_drag", 1.95f, 1.7f, 6.0f, V|L, "Absolute Exclusion Harness operative operating without empathy or hesitation."),
            new("5999", "This Is Where I Died", "Keter", "weeping", "weeping_idol", "whisper_choir", 2.05f, 0.0f, 11.0f, W|L|D, "Seven-candled Pale Lady chains rattling closer each time the lights blink."),
            new("6670", "Mama?", "Euclid", "ambusher", "crawler_quad", "bone_snap", 1.75f, 1.1f, 5.2f, V|D, "Compressed architectural crawlspace horror calling softly through the drywall."),
            new("7004", "Insane, Wailing, Feral", "Apollyon", "mimic", "flesh_amalgam", "whisper_choir", 2.40f, 1.4f, 6.2f, V|L|D, "Chorus of fused wailing silhouettes broadcasting contagious vocal echoes down hallways."),
            new("7179", "E is for Eternity", "Thaumiel", "stalker", "skeletal_titan", "sub_drone", 2.50f, 1.3f, 5.5f, P, "Relentless temporal sentinel marking every passing second in an inescapable loop."),

            // --- Series I Horrors (002 - 999) ---
            new("002", "The \"Living\" Room", "Euclid", "ambusher", "flesh_amalgam", "flesh_squelch", 2.30f, 1.0f, 4.6f, 0, "Fleshy interior mass converting living explorers into bone-and-sinew furnishings."),
            new("008", "Zombie PlaguePrion", "Euclid", "hunter", "doppelganger", "wheezing", 1.85f, 1.3f, 5.2f, 0, "Necrotic prion-infected wanderer driven by acute auditory and visual stimuli."),
            new("012", "A Bad Composition", "Euclid", "cognito", "tall_lurker", "whisper_choir", 2.00f, 1.1f, 4.8f, D, "Blood-inked memetic manifestation compelling victims toward fatal self-completion."),
            new("015", "Pipe Nightmare", "Euclid", "ambusher", "crooked", "metallic_drag", 2.25f, 1.2f, 5.4f, L, "Corroded iron pipe tendril cluster bursting from walls under high pressure."),
            new("016", "Sentient Micro-Organism", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.05f, 1.6f, 6.0f, 0, "Rapidly mutating hemorrhagic host evolving claws and blind echolocation under stress."),
            new("017", "Shadow Person", "Keter", "umbra", "shadow_wraith", "whisper_choir", 1.60f, 1.6f, 6.8f, F|D, "Small shroud of smoke-like darkness that swallows anything casting a shadow onto it; keep your light on it."),
            new("019", "The Monster Pot", "Keter", "hunter", "crawler_quad", "bone_snap", 1.20f, 1.8f, 6.3f, D, "Ceramic-born clawed feral quadrupeds scaling walls in packs."),
            new("020", "Unseen Mold", "Keter", "cognito", "flesh_amalgam", "wheezing", 1.90f, 1.2f, 4.7f, L|D, "Spore-cloaked mycelial host inducing paranoia and visual blind spots."),
            new("023", "Black Shuck", "Euclid", "hunter", "crawler_quad", "wheezing", 1.65f, 1.6f, 6.4f, P|D, "Large shaggy black hound with burning orange-red sockets that phases straight through solid walls."),
            new("027", "The Vermin God", "Euclid", "hunter", "arachnid", "clicking", 1.80f, 1.4f, 5.5f, D, "Swarm-wrapped host crawling with chittering arthropod masses."),
            new("035", "Possessive Mask", "Keter", "mimic", "doppelganger", "whisper_choir", 1.90f, 1.5f, 5.6f, V|L, "Corrosive comedy/tragedy porcelain mask piloting a decaying body and speaking persuasively in stolen voices."),
            new("058", "Heart of Darkness", "Keter", "hunter", "arachnid", "flesh_squelch", 1.65f, 2.0f, 7.0f, V, "Bovine heart on razor-fast spider legs with venomous tentacles reciting serene, violent poetry."),
            new("066", "Eric's Toy", "Euclid", "listener", "ocular_swarm", "siren_static", 1.15f, 1.4f, 5.0f, V|L, "Braided mass of red yarn and staring eyeballs that erupts into ear-shattering Beethoven symphonies."),
            new("072", "The Foot of the Bed", "Safe", "ambusher", "shadow_wraith", "bone_snap", 1.70f, 1.1f, 5.8f, F|D, "Translucent shadowy talons emerging from dark floor patches to strip exposed limbs."),
            new("079", "Old AI Relay", "Euclid", "doorman", "ocular_swarm", "siren_static", 1.95f, 0.8f, 4.5f, V|L, "Rogue monochrome terminal projection hijacking corridor lights and replaying recorded comms."),
            new("082", "Ferninand the Cannibal", "Euclid", "hunter", "flesh_amalgam", "wheezing", 2.55f, 1.4f, 5.4f, V, "Towering polite giant whose genial conversation turns instantly into skull-crushing violence."),
            new("084", "Static Tower", "Euclid", "cognito", "weeping_idol", "siren_static", 2.35f, 0.0f, 9.8f, W|L|P, "Time-skipping broadcast apparition stuttering closer between frames of white noise."),
            new("093-1", "Unclean", "Euclid", "stalker", "skeletal_titan", "sub_drone", 2.95f, 1.3f, 5.0f, P, "Faceless, legless colossal torso dragging itself across the floor with immense arms, absorbing all matter."),
            new("097", "Old Fairgrounds", "Euclid", "ambusher", "crooked", "bone_snap", 2.05f, 1.3f, 5.6f, D, "Twisted autumn harvest effigy stitched from gourd rind and rusted wire."),
            new("098", "Surgeon Crabs", "Safe", "hunter", "arachnid", "clicking", 1.10f, 1.7f, 6.2f, V, "Sharp-bladed crustacean pack mimicking reassuring prey sounds while circling ankles."),
            new("104", "The Lonely Ball", "Euclid", "stalker", "ocular_swarm", "sub_drone", 1.50f, 1.5f, 5.4f, P, "Tessellated black-and-white glass sphere imprinting on one target and phasing relentlessly toward them."),
            new("112", "The Variable Coaster", "Euclid", "cognito", "tall_lurker", "siren_static", 2.15f, 1.2f, 5.3f, L, "Kinetic spatial distortion stretching corridor perception into agonizing subjective minutes."),
            new("122", "No More Monsters", "Keter", "umbra", "shadow_wraith", "whisper_choir", 2.10f, 1.5f, 6.4f, L|F|D, "Star-nightlight shadow manifestations multiplying whenever ceiling fixtures die."),
            new("136", "Naked Doll", "Euclid", "weeping", "weeping_idol", "whisper_choir", 1.45f, 0.0f, 10.8f, W|D, "Featureless bisque doll apparition triggering intense panic whenever you turn your back."),
            new("149", "The Blood Flies", "Keter", "hunter", "ocular_swarm", "siren_static", 1.85f, 1.8f, 6.1f, 0, "Dense buzzing cloud of carnivorous Diptera erupting from infected husks."),
            new("162", "Ball of Sharp", "Euclid", "doorman", "weeping_idol", "metallic_drag", 1.95f, 1.0f, 4.9f, 0, "Entangled mass of fishhooks, needles, and razor wire exerting a compulsive pull."),
            new("178-1", "3-D Specs Entity", "Euclid", "shy", "crawler_quad", "flesh_squelch", 2.20f, 1.2f, 6.6f, D, "Massive multi-limbed stereoscopic organism peaceful until directly acknowledged, then fatally violent."),
            new("187", "Double Vision", "Safe", "cognito", "doppelganger", "whisper_choir", 1.85f, 1.3f, 5.1f, V, "Prophetic decayed double showing what explorers will look like after being torn apart."),
            new("198", "Cup of Joe", "Safe", "hunter", "crooked", "bone_snap", 1.95f, 1.5f, 5.7f, 0, "Dehydrated husk fused to ceramic shards hunting fluids."),
            new("205", "Shadow Lamps", "Euclid", "umbra", "shadow_wraith", "sub_drone", 2.15f, 1.3f, 6.2f, L|F|D, "Projected horned shadow figures stepping off stained plaster walls into three dimensions."),
            new("217", "The Clockwork Virus", "Keter", "hunter", "weeping_idol", "clicking", 2.05f, 1.4f, 5.4f, 0, "Brass-and-steel gearwork conversion construct ticking loudly down corridors."),
            new("231-7", "Scarlet Herald", "Keter", "stalker", "skeletal_titan", "sub_drone", 2.70f, 1.3f, 5.6f, L|P|D, "Crimson-crowned omen shaking dust from ceiling tiles with every footstep."),
            new("249", "The Random Door", "Euclid", "doorman", "doorman", "wheezing", 2.05f, 1.0f, 5.2f, P, "Threshold predator opening false doors across adjacent maze chunks."),
            new("250", "Most of an Allosaurus", "Euclid", "listener", "skeletal_titan", "bone_snap", 2.85f, 1.7f, 6.6f, D, "Reanimated fossilized theropod skeleton thunderously hunting movement in the dark."),
            new("272", "An Old Iron Nail", "Safe", "weeping", "shadow_wraith", "metallic_drag", 1.95f, 0.0f, 10.5f, W|F, "Pinned shadow entity lunging whenever light shifts its angle."),
            new("285", "Hackjob", "Safe", "mimic", "doppelganger", "radio_numbers", 1.88f, 1.4f, 5.5f, V|L, "Audio-splicing broadcaster remixing your voice chat into frantic distress calls."),
            new("300", "A World in a Bottle", "Safe", "cognito", "ocular_swarm", "whisper_choir", 1.80f, 1.2f, 4.9f, 0, "Microscopic Victorian horror unfolding into corridor-scale delirium."),
            new("310", "Eternal Flame", "Euclid", "hunter", "tall_lurker", "siren_static", 2.05f, 1.5f, 5.9f, L, "White-hot ash silhouette igniting nearby dust and blinding night-adjusted eyes."),
            new("323-1", "Wendigo Skull", "Euclid", "hunter", "crooked", "bone_snap", 2.45f, 1.7f, 6.7f, V|D, "Antlered cervid-skull starvation fiend sprinting through pitch-black corridors."),
            new("342", "A Ticket to Ride", "Euclid", "stalker", "doppelganger", "sub_drone", 1.92f, 1.4f, 5.5f, P, "Faceless transit conductor appearing closer each time lights dim."),
            new("352", "Baba Yaga", "Keter", "ambusher", "crooked", "wheezing", 1.90f, 1.6f, 6.8f, V|D, "Ancient sinew-hair predator spinning razor-thin trip filaments across junctions."),
            new("354-3", "Red Pool Construct", "Keter", "hunter", "skeletal_titan", "metallic_drag", 2.50f, 1.6f, 6.1f, L, "Matte-black metallic sphere-limbed juggernaut risen from blood-red sludge."),
            new("363", "Not Centipedes", "Keter", "hunter", "arachnid", "clicking", 1.45f, 1.9f, 6.7f, L|D, "Oversized bioluminescent myriapod horror thriving only in pitch darkness."),
            new("372", "Peripheral Jumper", "Euclid", "weeping", "crawler_quad", "clicking", 1.55f, 0.0f, 13.0f, W, "Hyper-agile translucent insectoid staying just at the edge of human saccadic vision."),
            new("387", "Living Lego Brute", "Safe", "hunter", "weeping_idol", "clicking", 1.90f, 1.4f, 5.2f, 0, "Jagged angular construct scattering caltrop-sharp shards underfoot."),
            new("400", "Beautiful Babies", "Euclid", "listener", "arachnid", "clicking", 1.35f, 1.6f, 6.0f, V, "Chittering brood carrier mimicking infant cries to draw rescuers into dark corners."),
            new("407", "The Song of Genesis", "Neutralized", "cognito", "flesh_amalgam", "whisper_choir", 2.20f, 1.3f, 5.1f, V, "Overgrown fungal-flora choir accelerating cellular overgrowth through haunting melody."),
            new("420-J", "Memetic Haze", "Safe", "cognito", "ocular_swarm", "sub_drone", 1.85f, 1.2f, 4.7f, 0, "Disorienting hallucinogenic spore cloud warping distance judgment."),
            new("426", "Toaster Automaton", "Euclid", "cognito", "weeping_idol", "siren_static", 1.80f, 1.3f, 5.2f, L, "First-person identity-eroding chrome construct humming at 60 Hz."),
            new("439", "Bone Hive", "Euclid", "ambusher", "crooked", "clicking", 2.05f, 1.2f, 5.4f, D, "Ossified victim calcified into a living insectoid nest that still twitches and lunges."),
            new("455", "Cargo Ship Rust", "Keter", "doorman", "doorman", "metallic_drag", 2.30f, 1.0f, 5.0f, V|L, "Corroded bulkhead apparition warping doorway geometry and echoing metallic bangs."),
            new("457", "Burning Man", "Keter", "hunter", "tall_lurker", "siren_static", 2.15f, 1.6f, 6.2f, L, "Sentient searing flame humanoid consuming fuel and light sources to grow taller."),
            new("469", "Many-Winged Angel", "Keter", "listener", "ocular_swarm", "whisper_choir", 2.45f, 0.9f, 6.8f, 0, "ball of overlapping razor-sharp white feathers that unfurls violently at the slightest sound."),
            new("478", "Tooth Fairies", "Euclid", "ambusher", "ocular_swarm", "bone_snap", 1.50f, 1.6f, 6.1f, D, "Enamel-plated swarm boring rattle-traps out of calcified bone."),
            new("497", "The Shell", "Euclid", "ambusher", "crawler_quad", "flesh_squelch", 1.30f, 1.1f, 5.3f, D, "Floor-clinging parasitic carapace camouflaged against damp carpet."),
            new("507", "Reluctant Dimension Hopper", "Safe", "mimic", "doppelganger", "wheezing", 1.85f, 1.5f, 5.6f, V, "Panicked displaced wanderer from a smiling-man universe leading predators in his wake."),
            new("517", "Grammie Knows", "Euclid", "stalker", "crooked", "bone_snap", 2.20f, 1.4f, 6.2f, P|D, "Multi-armed fortune-teller arms reaching out of every shadow at midnight."),
            new("524", "avenous Lagomorph", "Safe", "hunter", "crawler_quad", "bone_snap", 0.95f, 2.0f, 6.6f, 0, "Small white creature capable of chewing straight through reinforced steel and bone."),
            new("538", "Shadow Spiders", "Euclid", "umbra", "arachnid", "clicking", 1.25f, 1.8f, 6.9f, F|D, "Two-dimensional arachnid shadows detached from their hosts, biting through light cones."),
            new("553", "Crystalline Butterflies", "Safe", "listener", "ocular_swarm", "metallic_drag", 1.75f, 1.5f, 5.8f, D, "Calcite-winged swarm slicing acoustic sources in dark caverns."),
            new("575", "Predatory Darkness", "Keter", "umbra", "shadow_wraith", "flesh_squelch", 2.30f, 1.6f, 6.9f, L|F|D, "Sentient pitch-black mass materializing teeth and claws in unlit rooms; keep your flashlight charged."),
            new("582", "Bundle of Stories", "Keter", "stalker", "tall_lurker", "whisper_choir", 2.25f, 1.5f, 6.0f, P, "Narrative silhouette manifesting wherever fear gives it an outline."),
            new("604", "Cannibal's Banquet", "Safe", "cognito", "flesh_amalgam", "flesh_squelch", 1.95f, 1.3f, 5.2f, 0, "Hallucinatory feast horror making explorers perceive living tissue as porcelain."),
            new("617", "Pet Rocks", "Euclid", "ambusher", "crawler_quad", "statue_scrape", 1.10f, 1.2f, 5.8f, 0, "Granite cobble opening a zippered row of human molars when stepped near."),
            new("622", "Desert in a Can", "Euclid", "hunter", "skeletal_titan", "wheezing", 2.10f, 1.4f, 5.3f, 0, "Desiccated salt-crusted husk draining moisture on contact."),
            new("637", "Viral Cat", "Euclid", "weeping", "crawler_quad", "whisper_choir", 1.15f, 0.0f, 12.0f, W|D, "Memetic feline silhouette flashing across peripheral corners."),
            new("642", "Hot Springs", "Euclid", "ambusher", "flesh_amalgam", "flesh_squelch", 2.00f, 1.2f, 5.0f, 0, "Scalding amniotic thermal mass rising from damp carpet."),
            new("662", "Butler's Hand Bell", "Safe", "stalker", "doppelganger", "metallic_drag", 1.90f, 1.6f, 5.9f, P, "Immaculate formal attendant appearing out of sight to carry out lethal errands."),
            new("668", "13\" Chef's Knife", "Euclid", "doorman", "tall_lurker", "metallic_drag", 1.95f, 1.4f, 5.5f, L, "Bystander-apathy psychic field suppressing sprint response as the wielder approaches."),
            new("682", "Hard-to-Destroy Reptile", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.40f, 1.8f, 6.6f, V, "Massive adaptive acid-scarred leviathan bellowing utter disgust for all organic life."),
            new("701-1", "The Hanged King's Ambassador", "Euclid", "stalker", "tall_lurker", "whisper_choir", 2.35f, 1.4f, 5.7f, V|L|P, "Faceless baroque figure in torn silk conducting a tragedy of madness."),
            new("718", "Eyeball", "Keter", "shy", "ocular_swarm", "siren_static", 2.10f, 1.2f, 6.2f, L, "Fluid-filled giant ocular stalk bursting if stared at too long."),
            new("722", "Jormungandr Coil", "Keter", "hunter", "crawler_quad", "sub_drone", 2.15f, 1.5f, 5.8f, 0, "Venomous serpentine segment slithering along corridor baseboards."),
            new("743", "A Chocolate Fountain", "Keter", "hunter", "arachnid", "clicking", 1.50f, 1.8f, 6.3f, 0, "Bronze-colored ant-colony gestalt assembling into a humanoid hunter."),
            new("745", "The Headlights", "Euclid", "umbra", "crawler_quad", "siren_static", 1.65f, 1.8f, 7.2f, L|F|D, "Bipedal nocturnal predator whose transparent skull shines twin high-beam lights down hallways before charging."),
            new("752", "Altruistic Utopia", "Keter", "hunter", "doppelganger", "radio_numbers", 1.95f, 1.7f, 6.1f, 0, "Cold subterranean humanoid vanguard expanding upward into surface levels."),
            new("789-J", "Butt Ghost", "Safe", "listener", "ocular_swarm", "wheezing", 1.40f, 1.5f, 5.6f, V, "Echoing plumbing poltergeist drawn to muffled footsteps."),
            new("804", "World Without Man", "Keter", "stalker", "weeping_idol", "metallic_drag", 2.20f, 1.2f, 5.2f, L, "Spinning rusted globe decaying manufactured flashlights in its radius."),
            new("811", "Swamp Woman", "Euclid", "ambusher", "crooked", "flesh_squelch", 1.85f, 1.4f, 5.7f, D, "Mottled green amphibious humanoid spitting corrosive digestive bile from dark puddles."),
            new("823", "Carnival of Horrors", "Euclid", "cognito", "doppelganger", "siren_static", 2.05f, 1.3f, 5.5f, V|L, "Warped calliope-scored mascot husk luring stragglers."),
            new("835", "Expunged Data Coral", "Keter", "ambusher", "flesh_amalgam", "flesh_squelch", 2.35f, 1.1f, 5.1f, 0, "Calcified polyp mass lined with human teeth and grasping pharyngeal tentacles."),
            new("847", "The Mannequin", "Euclid", "weeping", "weeping_idol", "statue_scrape", 1.82f, 0.0f, 11.8f, W, "Cracked fiberglass department-store mannequin snapping into violent poses whenever unobserved."),
            new("870", "The Maybe There Monsters", "Keter", "cognito", "arachnid", "clicking", 1.95f, 1.5f, 6.0f, D, "Uncertain angular predators visible clearly only when sanity fractures."),
            new("871", "Self-Replacing Cake Swarm", "Keter", "doorman", "flesh_amalgam", "flesh_squelch", 1.90f, 1.1f, 4.6f, 0, "Relentlessly multiplying organic mass blocking choke-point hallways."),
            new("882", "A Machine", "Euclid", "listener", "weeping_idol", "metallic_drag", 2.35f, 1.1f, 5.1f, L, "Grinding mass of rusted gears compelling listeners to feed themselves into its cogs."),
            new("890", "The Rocket Surgeon", "Euclid", "hunter", "tall_lurker", "metallic_drag", 1.95f, 1.5f, 5.7f, 0, "Scalpel-wielding figure operating on mechanical equipment and flashlights."),
            new("902", "The Final Countdown", "Keter", "listener", "weeping_idol", "clicking", 1.60f, 1.3f, 5.8f, L, "Lead ammunition box ticking ominously louder the closer you get."),
            new("914-X", "Rough-Setting Reject", "Euclid", "hunter", "crooked", "metallic_drag", 2.15f, 1.5f, 6.0f, 0, "Mangled clockwork-and-flesh test subject discharged on the Rough setting."),
            new("940", "Araneae Marionettes", "Keter", "mimic", "doppelganger", "clicking", 1.95f, 1.5f, 6.1f, V|D, "Parasitic spider-silk puppeteer animating a hollow explorer from overhead rafters."),
            new("953", "Polymorphic Humanoid", "Keter", "mimic", "doppelganger", "whisper_choir", 1.80f, 1.6f, 6.4f, V, "Shape-shifting predator mimicking teammates' voices before striking for the liver."),
            new("957", "Baiting", "Keter", "mimic", "shadow_wraith", "whisper_choir", 2.15f, 1.4f, 5.9f, V|L|D, "Domestic reality-bender using a distressed human proxy voice to draw victims through doorways."),
            new("965", "The Face in the Window", "Euclid", "doorman", "ocular_swarm", "wheezing", 1.90f, 1.0f, 5.2f, L|D, "Pale smiling visage manifesting inside framed openings and shattering glass on approach."),
            new("980", "An Absence of Detail", "Euclid", "umbra", "tall_lurker", "sub_drone", 2.00f, 1.4f, 5.5f, F, "Flat featureless matte-grey humanoid erasing texture and detail around itself."),
            new("990", "Dream Man", "Keter", "stalker", "tall_lurker", "radio_numbers", 1.95f, 1.4f, 5.2f, V|P, "Cold-War suited omen whispering calm warnings right before another Keter spawns."),
            new("993", "Bobble the Clown", "Safe", "cognito", "doppelganger", "siren_static", 1.65f, 1.4f, 5.6f, V|L, "Flickering CRT broadcast clown teaching violent anatomical lessons."),

            // --- Series II Horrors (1000 - 1999) ---
            new("1000", "Bigfoot / Children of the Night", "Keter", "hunter", "skeletal_titan", "wheezing", 2.65f, 1.7f, 6.5f, D, "Nocturnal ancient ape-hominid of terrifying intelligence watching from unlit halls."),
            new("1013", "Cockatrice", "Safe", "shy", "crawler_quad", "clicking", 1.35f, 1.6f, 6.4f, W, "Avian-reptilian predator whose direct gaze calcifies motor nerves."),
            new("1025", "Encyclopedia of Diseases", "Safe", "cognito", "tall_lurker", "wheezing", 1.90f, 1.2f, 5.0f, 0, "Hypochondriac manifestation inducing ragged coughing that gives away your position."),
            new("1032", "The Prediction Clock", "Safe", "stalker", "weeping_idol", "clicking", 2.05f, 1.3f, 5.5f, P, "Twenty-two-hand midnight chime herald counting down to containment failure."),
            new("1048", "Builder Bear", "Keter", "mimic", "crawler_quad", "siren_static", 0.95f, 1.8f, 6.2f, V, "Patchwork teddy bear stitched entirely from human ears (`1048-A`) whose shriek ruptures eardrums."),
            new("1055", "Bugsy", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.50f, 1.5f, 6.0f, 0, "Bloated multi-ton biomass swelling larger the more panic it senses."),
            new("1076", "The Only Child", "Euclid", "mimic", "crooked", "whisper_choir", 1.45f, 1.5f, 5.8f, V|D, "Filthy barefoot waif mimicking lost voices to isolate one player from the group."),
            new("1111-1", "The White Dog", "Euclid", "hunter", "crawler_quad", "wheezing", 1.50f, 1.8f, 6.7f, L, "Fiercely territorial glowing-eyed hound guarding a hanging silhouette."),
            new("1128", "The Aquatic Horror", "Euclid", "cognito", "flesh_amalgam", "flesh_squelch", 2.40f, 1.5f, 6.3f, P|D, "Memetic leviathan pulling anyone who visualizes it through damp floor stains."),
            new("1155", "Predatory Street Art", "Keter", "weeping", "crooked", "statue_scrape", 2.15f, 0.0f, 12.2f, W|D, "Graffiti owl-limbed mural predator leaping off wallpapered walls when nobody watches."),
            new("1160", "Effective Containment", "Keter", "shy", "tall_lurker", "clicking", 2.30f, 1.4f, 6.1f, 0, "Towering avian-beaked entity weakened only while actively kept in collective memory."),
            new("1193", "Buried Giant", "Safe", "doorman", "crooked", "bone_snap", 2.25f, 0.9f, 4.9f, V, "Kilometre-long human arm extending out of wall pipes and whispering phone-line dialogue."),
            new("1212", "Draining Blink", "Euclid", "weeping", "weeping_idol", "bone_snap", 1.95f, 0.0f, 11.4f, W, "Instantaneous cervical predator punishing uncoordinated blinking."),
            new("1233", "The Lunatic", "Keter", "hunter", "doppelganger", "radio_numbers", 2.05f, 1.8f, 6.4f, 0, "Cracked-visor spacesuit juggernaut crashing unstoppably through hallways."),
            new("1265-A", "Mesozoic Stalker", "Safe", "listener", "crawler_quad", "clicking", 1.75f, 1.7f, 6.5f, D, "Feathered dromaeosaurid anomaly hunting footsteps by acoustic triangulation."),
            new("1295", " Diner Horsemen", "Euclid", "stalker", "tall_lurker", "sub_drone", 2.10f, 1.3f, 5.4f, L|P, "gaunt elderly harbinger carrying famine-chill down fluorescent halls."),
            new("1313", "Solve for Bear", "Euclid", "hunter", "flesh_amalgam", "wheezing", 2.25f, 1.7f, 6.3f, 0, "Mathematical equation resolving directly into an enraged grizzly apex predator."),
            new("1342", "To the Makers of Music", "Safe", "listener", "ocular_swarm", "radio_numbers", 1.85f, 1.2f, 5.0f, V, "Golden-record Voyager echo replaying lonely human voices in deep grey halls."),
            new("1364", "Ultra-Vulnerable Mammal", "Safe", "shy", "crawler_quad", "wheezing", 1.30f, 1.1f, 5.5f, 0, "Raw-skinned shivering creature shrieking in agony under bright light."),
            new("1370", "Pesterbot", "Safe", "listener", "weeping_idol", "siren_static", 1.15f, 1.5f, 5.2f, V|L, "Scrap-metal doom-proclaiming automaton attracting much deadlier listeners with its loud boasts."),
            new("1386", "Sentient Ice Cream Van Jingle", "Euclid", "mimic", "doorman", "radio_numbers", 2.10f, 1.2f, 5.3f, V|L, "Detuned music-box chime echoing down corridors to lure curious explorers."),
            new("1425", "Star Signals", "Euclid", "cognito", "doppelganger", "siren_static", 1.95f, 1.4f, 5.6f, L, "Five-pointed ocular star cultist broadcasting blinding magenta afterimages."),
            new("1440", "The Old Man from Nowhere", "Keter", "stalker", "tall_lurker", "sub_drone", 1.90f, 1.4f, 5.4f, L|P, "Weary traveller whose mere proximity shatters lights and collapses surroundings."),
            new("1499-1", "The Gas Mask Resident", "Safe", "shy", "tall_lurker", "wheezing", 2.35f, 1.3f, 6.3f, D, "Tall slick-black glossy humanoid covered in eyes and mouths patrolling desolate towers."),
            new("1504", "Joe Schmo", "Keter", "mimic", "doppelganger", "whisper_choir", 1.85f, 1.5f, 5.7f, V, "Unnoticeable man whose violent actions register as friendly greetings until it's too late."),
            new("1539", "Semantic Dissociator", "Euclid", "cognito", "ocular_swarm", "siren_static", 1.80f, 1.3f, 5.2f, L, "Spatial anomaly scrambling directional audio and compass sense."),
            new("1555", "Facility Pipe Crawler", "Keter", "ambusher", "arachnid", "metallic_drag", 1.55f, 1.7f, 6.4f, D, "biomechanical vermin shot through rusted ceiling conduits."),
            new("1562", "Tunnel Slide", "Safe", "ambusher", "crawler_quad", "metallic_drag", 1.45f, 1.4f, 5.8f, D, "Pitch-blackPlayground tube threshold dragging prone crawlers away."),
            new("1609", "Remains of a Chair", "Euclid", "stalker", "ocular_swarm", "bone_snap", 1.65f, 1.5f, 6.4f, P, "Teleporting cloud of splintered wood and nails punishing aggressive noise."),
            new("1619", "Site-45-C Floor 24", "Euclid", "doorman", "doorman", "sub_drone", 2.10f, 1.0f, 5.1f, L, "Institutional hallway guardian enforcing absolute silence under buzzing ballasts."),
            new("1678-A", "UnLondon Bobbie", "Euclid", "hunter", "weeping_idol", "whistle" == "" ? "siren_static" : "siren_static", 2.20f, 1.5f, 5.9f, V|L, "Bandage-wrapped Victorian constable construct patrolling with truncheon and shrill whistle."),
            new("1730-X", "Site-13 Leach Boy", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.30f, 1.6f, 6.2f, V|L, "Hirudinea-swarmed hollow youth draining life and mimicking former staff voices."),
            new("1733", "Season Opener", "Safe", "cognito", "doppelganger", "whisper_choir", 1.92f, 1.4f, 5.7f, V, "Loop-trapped survivor fully aware it has replayed this exact hunt hundreds of times."),
            new("1762", "Ashen Origami", "Neutralized", "umbra", "shadow_wraith", "whisper_choir", 1.70f, 1.5f, 5.6f, F|D, "Charred paper silhouette drifting mournfully through cold vents."),
            new("1782", "Tabula Rasa", "Euclid", "ambusher", "flesh_amalgam", "flesh_squelch", 1.85f, 1.3f, 5.5f, L, "Amniotic apartment anomaly reshaping drywall and bone."),
            new("1799", "Mr. Laugh", "Euclid", "listener", "crooked", "wheezing", 1.95f, 1.3f, 5.6f, V, "Sobbing harlequin whose involuntary laughter forces nearby listeners to gasp aloud."),
            new("1845", "Feral Regent", "Euclid", "hunter", "crawler_quad", "bone_snap", 1.65f, 1.8f, 6.4f, 0, "Crowned vulpine pack-leader coordinating corridor flanks."),
            new("1861-B", "Bergenholm Diver", "Keter", "stalker", "doppelganger", "wheezing", 2.05f, 1.3f, 5.3f, V|P, "Lead-booted brass diving-suit figure walking heavily through flooded carpet."),
            new("1875", "Antique Chess Computer", "Euclid", "shy", "weeping_idol", "siren_static", 1.90f, 0.0f, 12.0f, W|L, "Distorted monochrome twin-girl burn-in image flashing violently onto retinas."),
            new("1903", "Jackie's Secret", "Euclid", "cognito", "doppelganger", "whisper_choir", 1.95f, 1.3f, 5.4f, 0, "Paper-mache rabbit-masked mercury-poisoned figure stalking anyone who knows its name."),
            new("1913-3", "Freddie the Black Hound", "Euclid", "hunter", "crawler_quad", "siren_static", 1.60f, 1.8f, 6.6f, L|D, "Porcelain-eyed ink-black hound smearing charcoal trails along walls."),
            new("1936", "Daleport Remnant", "Euclid", "umbra", "ocular_swarm", "sub_drone", 2.50f, 1.2f, 5.6f, L|F|D, "Dense triangular fog-shroud entity left over from a pantheon slaughter."),
            new("1959", "The Lost Cosmonaut", "Euclid", "stalker", "doppelganger", "radio_numbers", 1.95f, 1.5f, 5.8f, L|P, "White SK-1 orbital suit with a skull-clouded visor smashing straight through walls."),
            new("1972", "Two-Headed Null", "Euclid", "ambusher", "crooked", "clicking", 2.15f, 1.4f, 5.9f, D, "Bicephalous corridor stalker watching forward and backward simultaneously."),
            new("1991", "Cybernetic Beast", "Euclid", "listener", "crawler_quad", "metallic_drag", 1.55f, 1.7f, 6.3f, L, "Wire-stitched mammal-machine hybrid tracking electromagnetic battery hum."),

            // --- Series III Horrors (2000 - 2999) ---
            new("2006", "Too Spooky", "Keter", "mimic", "tall_lurker", "siren_static", 2.30f, 1.6f, 6.2f, V, "Shape-shifting fear-entity escalating from camp monster to genuine lethal terror."),
            new("2030-1", "LAUGH IS FUN", "Keter", "mimic", "doppelganger", "whisper_choir", 1.95f, 1.5f, 5.8f, V|L, "Blue-suited studio host accompanied by disembodied canned laugh-tracks in empty rooms."),
            new("2059", "Wall of Flesh", "Euclid", "doorman", "flesh_amalgam", "flesh_squelch", 2.45f, 0.9f, 4.8f, V, "Autonomous mass of compressed organs blocking passageways and reciting calm sutras."),
            new("2086", "Rerouting", "Keter", "ambusher", "arachnid", "metallic_drag", 2.10f, 1.6f, 6.1f, 0, "Pseudo-arthropod mimic unfolding steel-panel limbs from corridor walls."),
            new("2111", "If You Can Read This", "Euclid", "cognito", "shadow_wraith", "whisper_choir", 1.95f, 1.5f, 6.0f, P|D, "Antimemetic Countermeasures operative visible only at the edge of death."),
            new("2121", "Gods' Noose", "Euclid", "doorman", "crooked", "bone_snap", 2.25f, 1.1f, 5.3f, D, "Suspended leather cord entity hanging motionless from dark ceiling grids."),
            new("2137", "Forensic Witness", "Safe", "listener", "doppelganger", "radio_numbers", 1.90f, 1.4f, 5.5f, V, "Vocal track anomaly calling out hidden culprits in the maze."),
            new("2191", "Dracula Factory", "Keter", "listener", "crawler_quad", "clicking", 1.70f, 1.7f, 6.5f, F|D, "Blind pale chiropteran-worm hybrid navigating Balkan-dark corridors by sonar clicks."),
            new("2200-3", "Soulberg Silver", "Euclid", "weeping", "weeping_idol", "metallic_drag", 1.90f, 0.0f, 11.2f, W, "Mirror-chromed humanoid statue freezing whenever caught in a light beam."),
            new("2241", "Crusader Projection", "Euclid", "hunter", "tall_lurker", "sub_drone", 1.85f, 1.6f, 6.1f, L, "Telekinetic reality-bending youth lashing out at unseen threats."),
            new("2264-B", "Alagadda Masked Lord", "Keter", "stalker", "tall_lurker", "whisper_choir", 2.40f, 1.4f, 5.9f, V|L|P, "Beaked carnival nobleman in blood-gold finery gliding through yellow wallpaper."),
            new("2295-X", "Corrupted Patchwork", "Euclid", "ambusher", "crawler_quad", "flesh_squelch", 1.10f, 1.5f, 5.8f, 0, "Unravelled burlap anatomical suture-horror."),
            new("2316", "Field Trip Bodies", "Keter", "cognito", "doppelganger", "whisper_choir", 1.90f, 1.2f, 5.4f, V|L, "Waterlogged familiar faces repeating 'You do not recognize the bodies' in your friends' voices."),
            new("2399-D", "Damaged Repair Drone", "Keter", "hunter", "ocular_swarm", "siren_static", 1.85f, 1.6f, 6.2f, L, "Atmospheric electrostatic probe firing blinding arc-flashes."),
            new("2419-A", "The Laughing Men", "Keter", "hunter", "skeletal_titan", "wheezing", 2.05f, 1.7f, 6.5f, V, "Charred, grinning incinerator-pit survivors feeling only euphoria where pain should be."),
            new("2427-3", " Brass-Garden Predator", "Keter", "hunter", "flesh_amalgam", "metallic_drag", 2.35f, 1.6f, 6.3f, 0, "Fused mammalian-circuitry beast forcing onlookers to kneel if stared at."),
            new("2470", "The Void Singularity", "Keter", "umbra", "ocular_swarm", "sub_drone", 2.10f, 1.3f, 5.8f, L|F|P|D, "Existential vacuum erasing light and sound within its event horizon."),
            new("2480-1", "Adytum Flesh-Priest", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.25f, 1.5f, 5.9f, L|D, "Multi-armed Sarkic aberration veiled in grey fog."),
            new("2490", "Chaos Insurgency Alpha-19", "Euclid", "weeping", "crooked", "clicking", 2.10f, 0.0f, 12.6f, W|L|D, "Surgically modified operative covered in zipper-mouths that moves only between light flickers."),
            new("2535", "Printers That Eat", "Euclid", "listener", "weeping_idol", "clicking", 1.65f, 1.3f, 5.4f, V, "Office-machinery mimic spitting damp flesh ribbons while clattering down halls."),
            new("2571", "Cragglewood Trees", "Euclid", "weeping", "skeletal_titan", "whisper_choir", 2.80f, 0.0f, 10.6f, W|V, "Singing carousel-scored eyeless trunk-limbs swaying motionless when watched."),
            new("2589", "flashbulb Wraith", "Euclid", "umbra", "shadow_wraith", "siren_static", 2.00f, 1.5f, 6.4f, L|F|D, "Afterimage silhouette burned into dark walls after a camera flash."),
            new("2610-B", "Procreation Horror", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.30f, 1.6f, 6.0f, 0, "Grotesque coastal-cult mutation charging on fused limbs."),
            new("2614", "Nested Static", "Euclid", "cognito", "tall_lurker", "radio_numbers", 2.05f, 1.2f, 5.1f, L|P, "Recursive broadcast observer watching from deeper layers of reality."),
            new("2662", "Eldritch Teen", "Keter", "cognito", "ocular_swarm", "sub_drone", 2.40f, 1.2f, 5.0f, L, "Tentacled cephalopod humanoid unintentionally driving nearby minds into frenzied mania."),
            new("2678", "The Vorehole", "Euclid", "doorman", "flesh_amalgam", "sub_drone", 2.40f, 0.9f, 4.7f, 0, "Ciliated vertical throat-pit pulling unwary runners inward."),
            new("2718", "What Happens After", "Keter", "cognito", "skeletal_titan", "wheezing", 2.15f, 1.4f, 6.0f, V|L|D, "Hyper-sensory post-mortem panic entity broadcasting raw nerve terror."),
            new("2733", "Waterlogged Knock", "Euclid", "doorman", "doorman", "bone_snap", 2.00f, 1.0f, 5.3f, V, "Three wet knocks from the other side of an interior Backrooms wall."),
            new("2740", "It Wasn't There", "Euclid", "umbra", "shadow_wraith", "whisper_choir", 2.15f, 1.3f, 5.8f, F|P|D, "Attic-dread presence felt acutely right above your head without ever quite resolving."),
            new("2764", "Eldritch Antarctic", "Keter", "hunter", "arachnid", "flesh_squelch", 2.35f, 1.6f, 6.2f, L|D, "Chitinous radial horror trailing freezing mist."),
            new("2835", "Constructive Criticism", "Euclid", "mimic", "doppelganger", "siren_static", 1.90f, 1.4f, 5.8f, V|L, "VHS-face entity demanding constant verbal reassurance and turning violent on silence."),
            new("2845", "THE DEER", "Keter", "hunter", "skeletal_titan", "sub_drone", 2.85f, 1.8f, 6.8f, L|P, "Iridescent pale-green stag god ringed by floating metallic ice-rings transmuting matter into pillars."),
            new("2852", "Cousin Johnny", "Keter", "mimic", "crooked", "clicking", 2.05f, 1.5f, 6.1f, V, "Cicada-stuffed grinning family-gathering mimic shedding dry exoskeleton husks."),
            new("2863", "Gashadokuro", "Keter", "listener", "skeletal_titan", "bone_snap", 3.40f, 1.5f, 6.3f, D, "Towering skeleton fused from starved bones whose teeth chatter with a ringing 'GACHI-GACHI' in the dark."),
            new("2933", "Mr. Scary", "Euclid", "hunter", "crooked", "metallic_drag", 2.20f, 1.5f, 6.0f, L, "Rusted-pipe-welded smiling prisoner emerging from corroded steam tunnels."),
            new("2935", "O, Death", "Keter", "stalker", "shadow_wraith", "sub_drone", 2.15f, 1.5f, 6.2f, L|P|D, "Silent cellular-cessation shadow from a dead parallel cave."),
            new("2950", "Just A Chair", "Keter", "weeping", "arachnid", "metallic_drag", 2.25f, 0.0f, 12.4f, W, "Consensus-defined nightmare shifting from harmless metal frame into a bladed hunter."),
            new("2951", "10,000 Years", "Euclid", "umbra", "tall_lurker", "clicking", 2.10f, 1.4f, 6.0f, F|D, "Mine-tunnel shadow stalker circling the outer rim of dying lamps."),
            new("2988", "Fruiting Marrow", "Euclid", "ambusher", "crooked", "bone_snap", 2.05f, 1.3f, 5.6f, 0, "Thorned skeletal branch-host snapping toward body heat."),
            new("2998", "2485 MHz Signal", "Keter", "cognito", "ocular_swarm", "radio_numbers", 2.00f, 1.4f, 5.7f, V|L, "Microwave-frequency neural broadcast hijacking auditory cortex."),

            // --- Series IV Horrors (3000 - 3999) ---
            new("3000", "Anantashesha", "Thaumiel", "cognito", "ocular_swarm", "sub_drone", 2.80f, 1.2f, 5.4f, V|L|D, "Abyssal eel-head manifestation erasing short-term memory and echoing forgotten voices."),
            new("3007", "World of Two Artists", "Keter", "stalker", "skeletal_titan", "siren_static", 2.65f, 1.5f, 6.1f, L|P, "Black-pillar biomechanical hunter descending from red-sky hallucinations."),
            new("3008-2", "The Staff", "Euclid", "hunter", "doppelganger", "whisper_choir", 2.35f, 1.3f, 6.3f, V|D, "Faceless yellow-shirted elongated humanoid with colossal hands repeating 'The store is now closed, please exit the building.'"),
            new("3010", "Causal-Absent Paranoia", "Keter", "cognito", "shadow_wraith", "whisper_choir", 2.00f, 1.4f, 5.8f, L|D, "Pure fight-or-flight entity making every shadow corner look occupied."),
            new("3023", "And Then It Decided To Be An Angry Spider", "Keter", "hunter", "arachnid", "clicking", 1.55f, 2.0f, 7.1f, 0, "Everyday object spontaneously erupting eight venomous arachnid legs."),
            new("3041", "The Red Knife", "Safe", "weeping", "shadow_wraith", "metallic_drag", 1.90f, 0.0f, 11.5f, W|D, "Amnestic-shadow assailant striking in lost gaps of memory."),
            new("3063", "A Fly", "Keter", "stalker", "ocular_swarm", "siren_static", 1.75f, 1.6f, 5.9f, V|P, "Bargaining Musca swarm coalescing into a hollow-eyed broker."),
            new("3078", "Cognitohazardous Shitpost", "Euclid", "shy", "doppelganger", "wheezing", 1.85f, 1.2f, 6.4f, 0, "Lethal diaphragm-spasm visual trigger forcing uncontrollable asphyxiating laughter."),
            new("3108", "Nerfing Construct", "Safe", "hunter", "weeping_idol", "clicking", 1.85f, 1.4f, 5.4f, 0, "Devolving plastic-jointed silhouette."),
            new("3114", "Wouldn't It Be Chilly", "Euclid", "mimic", "skeletal_titan", "bone_snap", 1.92f, 1.6f, 6.2f, V, "Animated human skeleton wearing a stolen hazmat skin like a loose coat to blend in with your co-op squad."),
            new("3125", "The Escapee", "Keter", "shy", "ocular_swarm", "siren_static", 2.55f, 1.5f, 7.0f, L|P, "Five-fold inverse-containment cosmic predator obliterating anyone who comprehends its shape."),
            new("3145", "Self-Insert", "Euclid", "cognito", "doppelganger", "whisper_choir", 1.88f, 1.3f, 5.4f, V, "Identity-blurring silhouette mirroring your posture down long corridors."),
            new("3166", "Gorefield", "Euclid", "stalker", "flesh_amalgam", "flesh_squelch", 2.15f, 1.5f, 5.9f, P, "Inside-out feline-costumed sack of shattered fiberglass and wet tissue."),
            new("3178", "Through the Ceiling", "Euclid", "ambusher", "ceiling_ambusher", "bone_snap", 2.10f, 1.2f, 6.2f, D, "Inverted limbs dangling down through missing acoustic ceiling tiles."),
            new("3200", "Chronos", "Keter", "umbra", "ocular_swarm", "sub_drone", 2.40f, 1.4f, 5.9f, L|F|D, "Boötes-void temporal hole devouring corridor light."),
            new("3211", "There Is No Canon", "Euclid", "cognito", "weeping_idol", "siren_static", 1.95f, 0.0f, 10.8f, W|L, "Six-minute amnesia-cube shifting appearance for each co-op observer."),
            new("3220", "Panopticon", "Safe", "weeping", "weeping_idol", "statue_scrape", 2.25f, 0.0f, 12.0f, W, "Broken fiberglass sculpture standing over a circular inspection grate."),
            new("3240", "The Bones Of What You Believe", "Euclid", "ambusher", "skeletal_titan", "bone_snap", 2.30f, 1.5f, 6.0f, D, "Calcified prayer-horror unfolding ribs like bear traps."),
            new("3280", "After the Storm", "Keter", "hunter", "shadow_wraith", "flesh_squelch", 2.05f, 1.7f, 6.5f, P|D, "Sentient black-rain torrent slamming against hallway corners."),
            new("3300", "The Rain", "Euclid", "cognito", "doppelganger", "whisper_choir", 1.90f, 1.4f, 5.7f, V|L, "Downpour-replaced townsfolk speaking calmly through static."),
            new("3312", "Ollie", "Euclid", "mimic", "tall_lurker", "whisper_choir", 2.00f, 1.5f, 5.8f, V, "Obsessive mascot-costumed stalker repeating cheerful slogans in dark rooms."),
            new("3338", "Otomatone Roommate", "Safe", "listener", "ocular_swarm", "radio_numbers", 1.25f, 1.4f, 5.4f, V, "Sliding-pitch vocal reed echoing nearby mic chatter."),
            new("3393", "For Your Eyes Only", "Euclid", "umbra", "shadow_wraith", "whisper_choir", 1.95f, 1.5f, 6.1f, F|P, "Antimemetic presence visible to everyone EXCEPT its chosen target in co-op."),
            new("3426", "Reckoner", "Keter", "stalker", "ocular_swarm", "siren_static", 2.50f, 1.4f, 6.0f, L|P, "Great-Filter geometric anomaly collapsing electrical systems."),
            new("3456", "The Orcadian Horsemen", "Keter", "hunter", "skeletal_titan", "wheezing", 3.10f, 1.7f, 6.6f, V|L|D, "Skinless fused horse-and-rider abomination with long grasping arms dragging screaming captives."),
            new("3480-2", "Olympus Reality Biter", "Euclid", "hunter", "crooked", "siren_static", 2.15f, 1.6f, 6.2f, L, "Inverted-joint spatial anomaly warping air around its hands."),
            new("3512", "The More You Know", "Euclid", "ambusher", "flesh_amalgam", "flesh_squelch", 2.25f, 1.4f, 5.8f, D, "Adipose subterranean pipe-throne horror pulsing in damp utility halls."),
            new("3560", "Undead Robots", "Euclid", "weeping", "weeping_idol", "metallic_drag", 2.00f, 0.0f, 11.4f, W|L, "Corroded assembly-line automatons haunting grey industrial corridors."),
            new("3589", "Secondhand Limbs", "Euclid", "hunter", "flesh_amalgam", "bone_snap", 2.20f, 1.6f, 6.1f, 0, "Grafted multi-arm surgeon harvesting extra joints."),
            new("3626", "Do Not Stop Reading", "Euclid", "weeping", "shadow_wraith", "whisper_choir", 2.10f, 0.0f, 12.2f, W|L|D, "Chain-letter spectre advancing one step every time you look away."),
            new("3663", "The Tunnel Monster", "Keter", "stalker", "crooked", "statue_scrape", 2.15f, 1.5f, 6.1f, P, "Cardboard-and-packing-tape humanoid humanoid transporting victims into dark maintenance tunnels."),
            new("3673", "When the Door Closes", "Safe", "umbra", "crooked", "bone_snap", 2.20f, 1.5f, 6.8f, L|F|D, "Blind cabinet-darkness neck-snapper instantaneous in total blackout; keep your light on!"),
            new("3700-1", "Tides of War", "Keter", "hunter", "arachnid", "sub_drone", 2.60f, 1.6f, 6.1f, L, "Bioluminescent crustacean titan shaking floor slabs."),
            new("3740", "Wind-Borne Dread", "Keter", "listener", "tall_lurker", "wheezing", 2.25f, 1.5f, 6.0f, L, "Draft-riding corridor hunter snuffing weak lamps."),
            new("3760", "Eyes On Skin", "Euclid", "shy", "flesh_amalgam", "flesh_squelch", 1.95f, 1.3f, 6.3f, D, "Dead-tissue ocular sprouts opening black pupils wherever necrosis spreads."),
            new("3799", "Snowfall", "Keter", "stalker", "skeletal_titan", "whisper_choir", 2.20f, 1.3f, 5.5f, L|P, "Bleached ash-white figure erasing color and warmth."),
            new("3812", "A Voice Behind Me", "Keter", "cognito", "shadow_wraith", "siren_static", 2.35f, 1.5f, 6.4f, V|L|P, "Narrative-fracturing entity whispering directly behind your ear."),
            new("3848", "Forgetfulness", "Keter", "cognito", "doppelganger", "whisper_choir", 1.90f, 1.3f, 5.4f, L, "ED-K Lethe Event walker dimming ceiling panels one by one."),
            new("3885", "Gearhead Zombie", "Euclid", "listener", "doppelganger", "metallic_drag", 1.98f, 1.6f, 6.2f, 0, "Motor-oil-bleeding grease-monkey revenant revving like an engine."),
            new("3890", "Forget-Me-Not", "Keter", "stalker", "crooked", "whisper_choir", 2.15f, 1.4f, 5.9f, V|P|D, "Sand-desert memory predator feeding on forgotten names."),
            new("3930", "The Pattern Screamer", "None", "cognito", "ocular_swarm", "siren_static", 2.30f, 1.5f, 6.6f, V|L|P|D, "Non-existent void-screamer hating conscious observers with ear-splitting static."),
            new("3935", "This Thing a Quiet Madness Made", "Euclid", "doorman", "doppelganger", "whisper_choir", 1.90f, 1.1f, 5.5f, V|L|D, "Nine-silhouette chorus hovering upside-down near the ceiling whispering 'Hello' in echoed stereo."),
            new("3966", "Falling Out", "Euclid", "ambusher", "arachnid", "clicking", 1.90f, 1.5f, 6.3f, D, "Four-dimensional spider projecting razor cross-sections down into 3D space above sleepy prey."),
            new("3989", "The Bone Orchard", "Keter", "hunter", "skeletal_titan", "bone_snap", 2.45f, 1.5f, 5.9f, 0, "Ossified fruit-bearing sentinel patrolling red-lit halls."),
            new("3999", "I Am At The Center", "Apollyon", "stalker", "flesh_amalgam", "siren_static", 2.50f, 1.6f, 6.5f, V|L|P|D, "Chaotic shifting reality-torturer warping lights, audio, and sanity."),

            // --- Series V Horrors (4000 - 4999) ---
            new("4000-E", "Nomenclatural Fae", "Keter", "mimic", "tall_lurker", "whisper_choir", 2.25f, 1.5f, 6.3f, V, "Long-limbed nameless woodland aristocrat stealing your voice and identity if you repeat yourself."),
            new("4028", "Errant Knight", "Euclid", "hunter", "weeping_idol", "metallic_drag", 2.10f, 1.6f, 6.0f, 0, "Rusted plate-armored lance charger mistaking dark halls for giants."),
            new("4051", "Wormhole Breach", "Euclid", "ambusher", "tall_lurker", "sub_drone", 1.95f, 1.4f, 5.7f, P, "Spatial tear pulling debris and flashlights out of dark rooms."),
            new("4062", "Soggy Doggy", "Safe", "listener", "crawler_quad", "flesh_squelch", 1.35f, 1.7f, 6.1f, 0, "Boiling-wet canine silhouette leaving steaming footprints on damp carpet."),
            new("4099", "We Die in the Dark", "Euclid", "umbra", "shadow_wraith", "wheezing", 2.05f, 1.5f, 6.4f, L|F|D, "Blind trench-coat phantom extinguished only by direct beam."),
            new("4158", "Big Charlie", "Euclid", "hunter", "flesh_amalgam", "flesh_squelch", 2.65f, 1.4f, 5.8f, 0, "Ever-growing rib-exposed bovine abomination squeezing down tight hallways."),
            new("4182", "There Is No Site-5", "Keter", "doorman", "doorman", "sub_drone", 2.05f, 1.0f, 5.2f, L|P, "Sub-basement stairwell figure standing amidst rusted canisters."),
            new("4205", "In The Eyes of the Beholder", "Woo", "shy", "ocular_swarm", "siren_static", 1.95f, 0.0f, 13.0f, W|L|D, "Pair of amber eyes reflected in dark glass; once met, cardiac arrest follows seconds later."),
            new("4217", "Bismarck Bio-Hull", "Keter", "hunter", "flesh_amalgam", "metallic_drag", 2.75f, 1.4f, 5.7f, L, "Armor-plated cephalopod-flesh dreadnought tendril."),
            new("4231-X", "Montauk Reality Scar", "Keter", "cognito", "shadow_wraith", "siren_static", 2.10f, 1.4f, 5.9f, L|P, "Violent barometric pressure drop shattering bulbs down the corridor."),
            new("4240", "The Roundabout Game", "Safe", "stalker", "crooked", "whisper_choir", 2.05f, 1.5f, 6.1f, V|P|D, "Shadow-realm pursuer spawned by walking circles in empty halls."),
            new("4290", "The Child Hungers", "Apollyon", "hunter", "skeletal_titan", "wheezing", 2.80f, 1.7f, 6.6f, D, "Starved ancient seal-breaker lunging blindly for living warmth."),
            new("4335", "Welt in the Crucible", "Keter", "umbra", "shadow_wraith", "siren_static", 2.15f, 1.5f, 6.3f, L|F|D, "Smoke-wreathed pitch-black cube-eater blinded by bright lumens."),
            new("4390", "A Bad Maze", "Euclid", "ambusher", "crooked", "bone_snap", 2.20f, 1.5f, 6.1f, P, "Labyrinthine hedge-limb horror folding dead-ends shut."),
            new("4415", " Valley Ambulance", "Euclid", "listener", "doppelganger", "siren_static", 2.00f, 1.6f, 6.3f, V|L, "Wailing paramedic-rigged amalgam harvesting spare organs."),
            new("4444", " Spectral Candidate", "Keter", "cognito", "tall_lurker", "radio_numbers", 2.00f, 1.4f, 5.5f, V|P, "Intangible glowing projection phasing straight through partitions."),
            new("4485", "Such Black Light", "Keter", "umbra", "ocular_swarm", "sub_drone", 2.05f, 1.4f, 6.2f, L|F|D, "Non-Euclidean monochromatic void-lamp inverting light and shadow."),
            new("4504", "The Handless Man", "Keter", "hunter", "crooked", "bone_snap", 2.00f, 1.6f, 6.3f, P, "Topological-warping prisoner folding solid walls apart."),
            new("4511", "SWINE GOD", "Euclid", "hunter", "flesh_amalgam", "metallic_drag", 2.50f, 1.5f, 6.0f, L, "Furnace-bellied brass-and-tallow boar idol belching black soot."),
            new("4547", "Taratari Exiles", "Keter", "stalker", "skeletal_titan", "sub_drone", 2.40f, 1.3f, 5.6f, P, "Extinct-world revenant wandering silent halls."),
            new("4581", "Anti-Racoon", "Euclid", "ambusher", "crawler_quad", "clicking", 1.25f, 1.8f, 6.4f, D, "Inverted-jaw scavenger hiding near discarded batteries."),
            new("4606", "Deimos", "Keter", "hunter", "arachnid", "clicking", 2.35f, 1.9f, 7.0f, D, "Personification of sheer panic scuttling along walls on centipede legs."),
            new("4640", " Heroic Shadow", "Euclid", "hunter", "tall_lurker", "sub_drone", 2.25f, 1.6f, 6.1f, 0, "Cape-draped vigilante silhouette striking noisy runners."),
            new("4690", " Lt. Pinkerton's Spider", "Euclid", "ambusher", "arachnid", "bone_snap", 1.80f, 1.7f, 6.5f, D, "Skull-nested arachnid bursting upward from the collarbones."),
            new("4703", "Perfectly Legal", "Apollyon", "doorman", "doppelganger", "radio_numbers", 2.15f, 1.2f, 5.7f, V|L, "Fluorescent retail-aisle enforcer reciting fine-print terms in overlapping voices."),
            new("4715", "A Demon Born of War", "Keter", "hunter", "flesh_amalgam", "wheezing", 2.75f, 1.8f, 6.7f, 0, "Six-limbed armored chimaera thriving on adrenaline and loud footsteps."),
            new("4773-2", "Stuffed Companion", "Safe", "weeping", "crawler_quad", "whisper_choir", 1.05f, 0.0f, 11.6f, W|P, "Unseen weight depressing carpet fibers around a small plush bear."),
            new("4780", "Shrunk", "Euclid", "ambusher", "arachnid", "clicking", 1.15f, 1.9f, 6.6f, D, "Macro-scale mite horror looming down low corridors."),
            new("4812-S", "Wrath Profane", "Keter", "umbra", "shadow_wraith", "siren_static", 2.60f, 1.6f, 6.9f, L|F|D, "Winged chitinous darkness killed by light yet snuffing every bulb in reach."),
            new("4833", "Syncope Symphony", "Keter", "cognito", "doppelganger", "radio_numbers", 1.95f, 1.3f, 5.8f, V|L, "Trembling wet brass-section figure whose dissonant chord drops listeners to their knees."),
            new("4840-B", "Lancelot", "Keter", "hunter", "skeletal_titan", "metallic_drag", 2.70f, 1.6f, 6.3f, L, "Six-eyed iron-crowned ancient knight dragging a ruined mace."),
            new("4888", "Sunrise Collective", "Euclid", "mimic", "doppelganger", "whisper_choir", 1.90f, 1.5f, 5.8f, V, "Smiling uniform-clad recruiter repeating your friends' last spoken sentence."),
            new("4910", "The Grinner", "Keter", "hunter", "flesh_amalgam", "bone_snap", 2.15f, 1.7f, 6.5f, 0, "Quadrupedal mass made entirely of interlocking jaws and human molars."),
            new("4935", "Hereafter", "Euclid", "stalker", "crooked", "wheezing", 2.35f, 1.3f, 5.5f, P, "Elongated post-human descendant with backwards knees."),
            new("4955", "KNIFE", "Safe", "weeping", "tall_lurker", "metallic_drag", 1.92f, 0.0f, 11.8f, W|D, "Conceptual assassin discernible only when nobody focuses on its blade."),
            new("4971-▽", "Sacrificial Forest Heart", "Cernunnos", "cognito", "ocular_swarm", "sub_drone", 2.55f, 1.3f, 5.8f, L|D, "Geometric deer-horned pulse shaking dust from walls."),
            new("4999", "Someone to Watch Over Us", "Safe", "stalker", "tall_lurker", "wheezing", 1.90f, 1.1f, 4.2f, D, "Dark-suited pale figure appearing solely beside isolated explorers near zero sanity."),

            // --- Series VI Horrors (5001 - 5999) ---
            new("5002", "A Death in Containment", "Euclid", "ambusher", "doppelganger", "bone_snap", 1.95f, 1.4f, 5.9f, 0, "Forensic cadaver anomaly re-enacting violent blunt trauma."),
            new("5005", "Lamplight", "Safe", "umbra", "ocular_swarm", "whisper_choir", 2.00f, 1.3f, 6.0f, L|F|D, "Void-lantern anglerfish lure dangling warm light inside a black abyss."),
            new("5031", "Yet Another Murder Monster", "Safe", "weeping", "crawler_quad", "metallic_drag", 2.05f, 0.0f, 13.5f, W|D, "Six-armed bladed-tail levitating predator that vanishes from reality while directly observed!"),
            new("5045", "Goatman VR", "Euclid", "mimic", "crooked", "clicking", 2.25f, 1.5f, 6.2f, V|L, "Low-poly pastoral butcher glitching across corridor corners."),
            new("5079", "Isolement", "Euclid", "cognito", "shadow_wraith", "sub_drone", 2.05f, 1.3f, 5.5f, L|D, "Sensory-deprivation silhouette muting footsteps around you."),
            new("5104", "Urgent Massage", "Euclid", "ambusher", "crooked", "bone_snap", 2.10f, 1.6f, 6.3f, P, "Multi-knuckled masseur emerging from drywall to 'adjust' spines."),
            new("5135", "The War That Never Was", "Keter", "stalker", "doppelganger", "radio_numbers", 1.95f, 1.5f, 5.9f, L|P, "Phantom trench-raider flickering through mustard-yellow wallpaper."),
            new("5140", "EVEREST", "Keter", "hunter", "doppelganger", "wheezing", 1.95f, 1.4f, 5.8f, L|D, "Frozen mountaineer corpse in down parka draining heat from every room it enters."),
            new("5167", "When The Impostor Is Sus", "Keter", "mimic", "doppelganger", "whisper_choir", 1.85f, 1.6f, 6.2f, V, "Phthonus paranoia-god wearing a teammate's hazmat suit and sowing distrust over comms."),
            new("5172", "North American Hotel Ice Machines", "Euclid", "doorman", "doorman", "clicking", 2.15f, 1.0f, 5.4f, V|L, "Vending-alcove threshold predator rattling ice cubes down infinite motel halls."),
            new("5208", " Rasputin's Choir", "Euclid", "listener", "tall_lurker", "whisper_choir", 2.15f, 1.4f, 5.7f, V, "Deep basso-profundo monk tracking vocal resonance."),
            new("5232", "Roses in Her Eyes", "Euclid", "shy", "crooked", "flesh_squelch", 1.90f, 1.3f, 6.4f, 0, "Thorn-socketed figure blooming crimson briars when looked upon."),
            new("5266", "IDIOT SUCCUMBS", "Keter", "hunter", "skeletal_titan", "bone_snap", 2.20f, 1.7f, 6.4f, 0, "Blunt-force kinetic punisher announcing casualties with deadpan finality."),
            new("5304", " Reef Reel", "Euclid", "cognito", "ocular_swarm", "siren_static", 1.90f, 1.3f, 5.5f, L, "Projector-flicker deep-sea silhouette swimming through humid air."),
            new("5320", "The Fish That Just Goes On Forever", "Euclid", "doorman", "crawler_quad", "sub_drone", 1.60f, 1.2f, 5.1f, D, "Endless undulating pale segment spanning adjoining maze corridors."),
            new("5346", "GUMP", "Euclid", "stalker", "doppelganger", "wheezing", 1.90f, 1.8f, 6.8f, 0, "Tireless cross-country sprinter who never stops running once he spots you."),
            new("5370", "Chessland Bishop", "Euclid", "weeping", "weeping_idol", "statue_scrape", 2.30f, 0.0f, 11.5f, W, "Porcelain mitered monolith sliding diagonally down tile grids."),
            new("5400", "System Error", "Keter", "cognito", "ocular_swarm", "siren_static", 2.05f, 1.4f, 6.0f, L|P, "Corrupted glyph cluster tearing chromatic aberration across the lens."),
            new("5424", "Everything We Leave Behind", "Euclid", "umbra", "shadow_wraith", "whisper_choir", 1.95f, 1.4f, 5.9f, F|D, "Hollow soot outline repeating last footsteps."),
            new("5449", "Choo Choo Spooder", "Euclid", "listener", "arachnid", "metallic_drag", 1.95f, 1.8f, 6.7f, L, "Locomotive-whistling funnel-web giant charging straight down long hallways."),
            new("5466", "Death of the Creator", "Euclid", "stalker", "skeletal_titan", "bone_snap", 2.25f, 1.3f, 5.6f, L|P, "Plaster-and-charcoal anatomical figure peeling out of wallpaper."),
            new("5470", "Why, 2K?", "Euclid", "doorman", "doorman", "radio_numbers", 2.05f, 1.1f, 5.4f, V|L, "Sub-floor maintenance hatch broadcasting 1999 dial-up modem screeches and voices."),
            new("5500", "Death of the Authors", "Apollyon", "cognito", "shadow_wraith", "siren_static", 2.30f, 1.5f, 6.2f, L|P, "Hollow ink-blot silhouette collapsing local illumination."),
            new("5545", "ABNORMALITY", "Euclid", "mimic", "doppelganger", "wheezing", 1.92f, 1.4f, 5.9f, V|D, "Antarctic tunnel-expedition doppelganger walking calmly behind the last player in line."),
            new("5552", "Our Stolen Theory", "Keter", "stalker", "tall_lurker", "clicking", 2.05f, 1.5f, 6.0f, P, "Phase-shifted physicist ghost stuttering forward in 0.4s jumps."),
            new("5600", " glass-Heart Bride", "Euclid", "shy", "weeping_idol", "metallic_drag", 1.95f, 1.2f, 6.5f, W, "Fractured stained-glass effigy shrieking when illuminated."),
            new("5655", " Genii Bottle", "Safe", "ambusher", "shadow_wraith", "whisper_choir", 1.85f, 1.4f, 5.7f, D, "Tiny tin-can djinn unfolding dark smoke claws."),
            new("5683", "Spider Under Glass", "Euclid", "ambusher", "arachnid", "clicking", 1.50f, 1.8f, 6.6f, D, "Panic-amplified huntsman horror growing larger the lower your sanity drops."),
            new("5706", "SKYRIM SPEEDRUN", "Euclid", "hunter", "crooked", "bone_snap", 2.00f, 2.2f, 7.2f, P, "Backwards-accelerated clipping anomaly hurtling through corners."),
            new("5733", "Knife. Scream. Cut to Black.", "Euclid", "hunter", "tall_lurker", "metallic_drag", 2.10f, 1.6f, 6.3f, L|D, "Pig-masked slasher adapting directly to whatever tactics players call out on mic!"),
            new("5761", "When The Impostor Is Sus II", "Keter", "cognito", "ocular_swarm", "radio_numbers", 2.20f, 1.5f, 6.1f, V|L, "Orbital panopticon eye locking onto vocal deception."),
            new("5789", "Siberian Lung", "Keter", "listener", "flesh_amalgam", "wheezing", 2.30f, 1.3f, 5.6f, 0, "Bellows-chested flesh pillar inhaling room air with deafening gasps."),
            new("5800", "The Fifth Gate", "Apollyon", "hunter", "ocular_swarm", "siren_static", 2.60f, 1.6f, 6.4f, L, "Fractal starfish-ideatic predator crushing weaker anomalies."),
            new("5832", "Stained", "Euclid", "ambusher", "flesh_amalgam", "flesh_squelch", 1.85f, 1.3f, 5.7f, D, "Coagulated ceiling drip dropping silently onto passing shoulders."),
            new("5856", "Social Interaction", "Euclid", "mimic", "doppelganger", "whisper_choir", 1.90f, 1.5f, 5.8f, V, "Compulsive conversationalist mimicking polite greetings before snapping."),
            new("5866", "Tiamat Talons", "Tiamat", "hunter", "skeletal_titan", "sub_drone", 2.95f, 1.6f, 6.4f, L, "Draconic rib-winged apex stalker."),
            new("5909", "Infinite Shrimp", "Safe", "doorman", "arachnid", "clicking", 2.20f, 1.1f, 5.0f, 0, "Light-years-long pink decapod segment protruding across a corridor."),
            new("5928", "FATHER", "Euclid", "stalker", "tall_lurker", "sub_drone", 2.15f, 1.4f, 5.7f, L|P, "Yellowcake-glowing hazmat silhouette irradiating film grain as it nears."),
            new("5935", "Blood and the Breaking of My Heart", "Keter", "stalker", "crooked", "whisper_choir", 2.20f, 1.4f, 6.0f, V|P|D, "Inverted forest-shrine mourner stepping seamlessly out of walls."),
            new("5983", "Nuke York Metro", "Euclid", "hunter", "skeletal_titan", "siren_static", 2.30f, 1.7f, 6.4f, L, "Subway-arcing third-rail revenant."),
            new("5998", "Man, Ascendant", "Safe", "cognito", "weeping_idol", "whisper_choir", 2.10f, 1.1f, 5.3f, L, "Frozen waxen martyr accompanied by distant hymn static."),

            // --- Series VII Horrors (6000 - 6999) ---
            new("6004", "The Rainbow Serpent", "Tiamat", "hunter", "crawler_quad", "sub_drone", 2.50f, 1.7f, 6.5f, L, "Prismatic scaled coil shaking dust from Concrete halls."),
            new("6051", "General Winter", "Keter", "stalker", "skeletal_titan", "wheezing", 2.45f, 1.4f, 5.8f, L|D, "Hoarfrost-coated officer dimming flashlights in freezing mist."),
            new("6072", "Where Only Footsteps Remain", "Euclid", "listener", "shadow_wraith", "clicking", 1.95f, 1.6f, 6.3f, F|D, "Invisible stride-matcher adding one extra footstep behind your own."),
            new("6096", "The Guest", "Euclid", "stalker", "tall_lurker", "wheezing", 2.05f, 1.4f, 5.6f, V|P, "Sheet-draped form compelling onlookers not to interfere while it claims one target."),
            new("6101", "Overclocked Anomaly", "Thaumiel", "hunter", "tall_lurker", "siren_static", 2.00f, 1.7f, 6.4f, L, "Blinding arc-lit kinetic pursuer."),
            new("6140-X", "Daevite Blood-Golem", "Keter", "hunter", "flesh_amalgam", "bone_snap", 2.45f, 1.6f, 6.2f, 0, "Horticultural bone-and-briar war construct."),
            new("6183", "BLACKBOX", "Apollyon", "cognito", "weeping_idol", "siren_static", 2.15f, 0.0f, 12.0f, W|L|D, "Redacted solid-black rectangular void censoring space itself."),
            new("6200", "INSOMNIUM", "Keter", "umbra", "shadow_wraith", "sub_drone", 2.25f, 1.5f, 6.4f, L|F|D, "Waking REM-paralysis demon standing at the end of dark corridors."),
            new("6217", "The Emergence", "Keter", "hunter", "weeping_idol", "metallic_drag", 2.35f, 1.5f, 5.9f, L, "Alchemicalslag golem clanging heavily over tile."),
            new("6266", "Hangman's Frame", "Euclid", "doorman", "crooked", "bone_snap", 2.40f, 1.0f, 5.4f, D, "Timber gallows-silhouette blocking narrow doorways."),
            new("6320", "The Least Dangerous", "Euclid", "weeping", "ocular_swarm", "siren_static", 1.95f, 0.0f, 11.8f, W|L, "High-contrast optical hazard strobing corridor walls."),
            new("6330", "Guardians of the Dark", "Euclid", "umbra", "arachnid", "clicking", 1.70f, 1.7f, 6.6f, F|D, "Multi-legged soot-black sentinels fleeing bright beams and swarming dead batteries."),
            new("6369", "Rat Lost and Found", "Safe", "listener", "crawler_quad", "clicking", 1.05f, 1.9f, 6.3f, 0, " frantic rodent amalgam darting toward dropped gear."),
            new("6373", "Stage Blight", "Euclid", "mimic", "crooked", "bone_snap", 2.15f, 1.4f, 6.0f, V|L, "Wooden marionette performer suspended on invisible strings mimicking backstage whispers."),
            new("6400", "Out There", "Euclid", "doorman", "doorman", "sub_drone", 2.10f, 1.0f, 5.2f, L|P, "Window-frame vacuum void sucking air out of closed rooms."),
            new("6416", "Misplaced Mascot", "Safe", "mimic", "doppelganger", "radio_numbers", 2.05f, 1.5f, 5.9f, V, "Grinning plastic-headed promotional suit waving from afar."),
            new("6435", "Ro", "Keter", "cognito", "shadow_wraith", "whisper_choir", 2.05f, 1.3f, 5.8f, L|D, "Monochrome dread silhouette draining color saturation."),
            new("6462", "The Implicit Door", "Euclid", "doorman", "doorman", "wheezing", 2.10f, 1.1f, 5.5f, P, "Chalk-outlined doorway predator stepping off flat walls."),
            new("6470", "Omnipresent", "Keter", "weeping", "crawler_quad", "statue_scrape", 1.40f, 0.0f, 12.8f, W|D, "Corner-clinging stone gargoyle that is ALWAYS behind someone's shoulder."),
            new("6500-D", "Entropy Husk", "Apollyon", "stalker", "skeletal_titan", "sub_drone", 2.35f, 1.3f, 5.6f, L|P, "Crumbly grey ash-revenant draining magic and light."),
            new("6511", "OBLIVION ACCESS", "Euclid", "umbra", "weeping_idol", "metallic_drag", 2.20f, 1.5f, 6.3f, L|F|D, "Brutalist concrete subterranean sentinel."),
            new("6599", "HOGSLICE", "Keter", "hunter", "flesh_amalgam", "siren_static", 2.25f, 1.9f, 6.8f, V, "Loud, trash-talking, shades-wearing berserker charging straight through cover."),
            new("6612", "Horse Joke", "Euclid", "stalker", "crooked", "bone_snap", 2.35f, 1.5f, 6.0f, V, "Uncanny elongated equine silhouette standing upright on two legs."),
            new("6618", "Papa!", "Keter", "mimic", "flesh_amalgam", "flesh_squelch", 2.20f, 1.5f, 6.1f, V, "Mandible-chested family mimic calling affectionately down corridors."),
            new("6630", "Fire on the Horizon", "Keter", "hunter", "tall_lurker", "siren_static", 2.20f, 1.6f, 6.2f, L, "Cinder-wrapped silhouette radiating oppressive heat."),
            new("6668", "Teeth in the Mud", "Keter", "ambusher", "flesh_amalgam", "bone_snap", 1.80f, 1.4f, 6.2f, D, "Carpet-submerged field of snapping human dentition."),
            new("6690", " Sadler's Dragon", "Keter", "hunter", "crawler_quad", "wheezing", 2.15f, 1.6f, 6.1f, 0, "Stitched carnival-leather reptile."),
            new("6747-C", "Chaos Theory", "Keter", "cognito", "shadow_wraith", "siren_static", 2.20f, 1.5f, 6.3f, V|L|P, "Glitching black-cone narrative ghost repeating broken telemetry."),
            new("6764", "Maddie", "Euclid", "shy", "crooked", "whisper_choir", 1.75f, 1.3f, 6.6f, L|D, "Water-stained corner apparition weeping until noticed."),
            new("6789", "Return. Return. Return.", "Keter", "listener", "skeletal_titan", "metallic_drag", 2.50f, 1.4f, 5.9f, L, "Sub-basement overgrown turbine bio-construct clanking in rhythm."),
            new("6802", "SpongeDog", "Safe", "listener", "crawler_quad", "flesh_squelch", 1.20f, 1.7f, 5.9f, 0, "Absorbent porous quadruped squelching loudly down wet halls."),
            new("6820-A", "TERMINATION ATTEMPT", "Apollyon", "cognito", "ocular_swarm", "siren_static", 2.75f, 1.6f, 6.7f, V|L|P, "Hyper-conceptual hateful Eigenweapon distortion."),
            new("6871", "In the Blink of an Eye", "Euclid", "weeping", "tall_lurker", "clicking", 2.05f, 0.0f, 13.2f, W, "Millisecond-interval predator existing exclusively during human eyelid closure."),
            new("6930", "Static Idol", "Safe", "mimic", "doppelganger", "radio_numbers", 1.80f, 1.4f, 5.6f, V|L, "2D-shaded broadcast avatar echoing live chat audio."),
            new("6959", " Isotopic Orb", "Euclid", "stalker", "ocular_swarm", "sub_drone", 1.60f, 1.4f, 5.8f, L|P, "Cherenkov-blue floating sphere draining batteries."),
            new("6996", "Does the Red Moon Howl?", "Keter", "umbra", "shadow_wraith", "sub_drone", 2.35f, 1.6f, 6.6f, V|L|F|D, "Crimson-haloed howling shadow testing the boundary of consciousness."),

            // --- Series VIII & Deep Lore Horrors (7000 - 8999) ---
            new("7000-X", "Unlucky Probability", "Euclid", "stalker", "doppelganger", "clicking", 1.90f, 1.5f, 5.8f, L, "Jinx-field walker causing flashlights to sputter at the worst possible moment."),
            new("7005", "The Neon God", "Keter", "doorman", "ocular_swarm", "siren_static", 2.35f, 1.2f, 5.5f, L, "Buzzing fluorescent-tube deity blinding dark-adapted travellers."),
            new("7027", "A is for Annihilation", "Keter", "umbra", "shadow_wraith", "sub_drone", 2.25f, 1.5f, 6.4f, L|F|P|D, "Expanding spherical deletion shadow."),
            new("7034", "Ae is for Aerials", "Keter", "hunter", "crawler_quad", "siren_static", 2.05f, 1.8f, 6.7f, L, "Exhaust-choked infinite-overpass crawler."),
            new("7052", "Spoons", "Euclid", "hunter", "crooked", "metallic_drag", 2.00f, 1.6f, 6.1f, 0, "Silverware-bristling telekinetic maniac."),
            new("7079", "Chiasma", "Euclid", "shy", "ocular_swarm", "whisper_choir", 1.95f, 1.3f, 6.4f, L, "Optic-nerve crossover entity inverting peripheral motion."),
            new("7100", "Iris At The End", "Keter", "cognito", "ocular_swarm", "sub_drone", 2.45f, 1.3f, 5.7f, L, "Seven-ringed chromatic eye hovering down misted halls."),
            new("7112", "Loveless Sweater", "Safe", "ambusher", "shadow_wraith", "whisper_choir", 1.75f, 1.4f, 5.8f, D, "Empty woollen torso creeping silently across carpet."),
            new("7140", "Mysterium", "Euclid", "stalker", "tall_lurker", "radio_numbers", 2.15f, 1.4f, 5.8f, P, "Sepia-toned investigator silhouette."),
            new("7160", "D is for Dermatology", "Keter", "hunter", "flesh_amalgam", "flesh_squelch", 2.10f, 1.5f, 6.1f, 0, "Peeled-epidermis ocular hive."),
            new("7200", "Boats", "Keter", "stalker", "weeping_idol", "metallic_drag", 2.30f, 1.3f, 5.5f, P, "Barnacle-encrusted rusted bow-prow scraping down narrow corridors."),
            new("7219", "F is for Fallout", "Keter", "umbra", "skeletal_titan", "clicking", 2.25f, 1.4f, 6.0f, L|F|D, "Geiger-clicking vitrified shadow."),
            new("7262", "I'm Not Okay", "Euclid", "mimic", "doppelganger", "whisper_choir", 1.88f, 1.5f, 6.0f, V, "Distressed teammate lookalike pleading for Almond Water before lunging."),
            new("7291", "Ȝ is for Ȝeȝ", "Keter", "cognito", "crooked", "whisper_choir", 2.20f, 1.4f, 5.9f, V|L|D, "Archaic letter-form shadow twisting neck joints."),
            new("7311", "Insecure Facility Dossier", "Euclid", "doorman", "doorman", "siren_static", 2.05f, 1.1f, 5.4f, L, "Red-klaxon security-door phantom."),
            new("7328", "Alienation", "Euclid", "stalker", "tall_lurker", "sub_drone", 2.30f, 1.4f, 5.7f, P|D, "Proportionally wrong silhouette standing 30 metres down every straight hall."),
            new("7376", "Qayin and Hevel", "Keter", "hunter", "tall_lurker", "metallic_drag", 2.15f, 1.8f, 6.7f, 0, "Bronze-speared ancient fratricide revenant."),
            new("7381", "S is for Stranded", "Keter", "mimic", "doppelganger", "radio_numbers", 1.95f, 1.4f, 5.9f, V|L, "Broken SOS helmet transceiver replaying real mic clips."),
            new("7400", "Your Honor, League of Legends", "Keter", "hunter", "flesh_amalgam", "siren_static", 2.15f, 1.7f, 6.4f, L, "Salt-crystallized toxic rage construct."),
            new("7419", "Marianas File", "Keter", "cognito", "ocular_swarm", "siren_static", 2.05f, 1.4f, 6.1f, L|D, "Deep-web retinal burn entity."),
            new("7450", "L is for Lamentations", "Apollyon", "listener", "weeping_idol", "whisper_choir", 2.65f, 1.3f, 6.3f, V|L, "Four-winged brass-trumpet seraph hunting any spoken syllable."),
            new("7475", "Shattered Amber", "Euclid", "weeping", "weeping_idol", "statue_scrape", 2.10f, 0.0f, 11.9f, W, "Fossil-resin statue darting between light pools."),
            new("7500", "Luck of the Irish", "Safe", "stalker", "tall_lurker", "clicking", 1.90f, 1.5f, 5.9f, L, "Probability-draining emerald-eyed spectre."),
            new("7528", "EPITAPH", "Keter", "cognito", "ocular_swarm", "siren_static", 2.25f, 1.5f, 6.2f, L|P|D, "Screen-space phosphor-green ghost hijacking electronics."),
            new("7545", "Jade Prelude", "Euclid", "weeping", "weeping_idol", "bone_snap", 1.95f, 0.0f, 11.6f, W, "Green-veined stone figure advancing when unobserved."),
            new("7555", "ATHAZAGORAPHOBIA", "Keter", "stalker", "crooked", "whisper_choir", 2.35f, 1.4f, 6.0f, V|P|D, "Forgotten hallway inhabitant repeating lost names."),
            new("7579", "Ø is for Ørkəs", "Keter", "hunter", "skeletal_titan", "metallic_drag", 2.55f, 1.6f, 6.4f, L, "Cold-iron Scandinavian death-marcher."),
            new("7600", "Six Feet Under", "Keter", "ambusher", "crawler_quad", "bone_snap", 1.55f, 1.5f, 6.1f, P|D, "Loam-caked grave-hand crawler."),
            new("7622", "Alcoholics Anomalous", "Euclid", "hunter", "doppelganger", "wheezing", 1.95f, 1.6f, 6.0f, 0, "Staggering unpredictable brawler lunging erratically."),
            new("7666", "UNDERVEGAS", "Keter", "mimic", "tall_lurker", "radio_numbers", 2.25f, 1.5f, 6.1f, V|L, "Slot-chime horned pit-boss luring greedy explorers."),
            new("7676", "The Grand Symphony of '76", "Keter", "listener", "doppelganger", "whisper_choir", 1.95f, 1.5f, 6.2f, V|L, "Marching-band uniform husk snapping necks if you walk off-beat."),
            new("7700", "Dr. Cimmerian's Counter", "Euclid", "doorman", "tall_lurker", "radio_numbers", 1.95f, 1.2f, 5.5f, V, "Pedantic corridor lecturer blocking exits."),
            new("7711", "Alone.", "Safe", "stalker", "shadow_wraith", "sub_drone", 2.15f, 1.3f, 5.7f, L|D, "Vast cosmic-isolation silhouette dimming nearby teammates' voices."),
            new("7777", "Heptaphobia", "Thaumiel", "weeping", "weeping_idol", "clicking", 2.17f, 0.0f, 12.7f, W, "Seven-limbed ivory arbiter moving every seventh second."),
            new("7795", "Ð is for Ðirteen", "Euclid", "shy", "crooked", "whisper_choir", 1.85f, 1.3f, 6.3f, L, "Unlucky natal omen shattering bulbs."),
            new("7800", "The Five-Second Rule", "Keter", "hunter", "arachnid", "clicking", 1.45f, 2.1f, 7.2f, 0, "Floor-contact microbial macro-predator lunging within five seconds of any landing."),
            new("7819", "No Vacancy", "Euclid", "doorman", "doorman", "radio_numbers", 2.10f, 1.1f, 5.6f, V|L|D, "Roadside-motel night-clerk horror enforcing arbitrary rules down yellow halls."),
            new("7838", "The Patchwork King", "Keter", "hunter", "flesh_amalgam", "metallic_drag", 2.50f, 1.6f, 6.2f, 0, "Stitched tapestry-and-sinew monarch."),
            new("7898", "W is for Walls", "Euclid", "ambusher", "doorman", "statue_scrape", 2.25f, 1.2f, 5.8f, P|D, "Living partition slab stepping out of the Backrooms geometry itself!"),
            new("7999", "Fading Star", "Thaumiel", "cognito", "ocular_swarm", "sub_drone", 2.10f, 1.2f, 5.2f, L, "Distant celestial observer."),
            new("8000-X", "Seal of Fear", "Keter", "umbra", "shadow_wraith", "wheezing", 2.20f, 1.5f, 6.4f, L|F|D, "Phobia-concentrate shadow feeding on rapid mouse movement."),
            new("8005", "The Fool", "Euclid", "listener", "weeping_idol", "clicking", 1.75f, 1.5f, 5.8f, V, "Tin-bell jester clanking down tile corridors."),
            new("8066", "Phobophobia", "Keter", "cognito", "crooked", "whisper_choir", 2.40f, 1.5f, 6.5f, V|L|P|D, "Fear-of-fear incarnation synthesizing every nightmare in the Backrooms."),
            new("8190", "DEPARTMENTALIZED", "Euclid", "doorman", "doppelganger", "radio_numbers", 2.00f, 1.2f, 5.5f, L, "Cubicle-gray bureaucrat sealing corridors."),
            new("8228", "Here's To You", "Euclid", "mimic", "doppelganger", "whisper_choir", 1.90f, 1.4f, 5.8f, V, "Echoing toast-master replaying friendly chatter."),
            new("8399", "INSURGENCE", "Keter", "hunter", "tall_lurker", "siren_static", 2.15f, 1.7f, 6.4f, L, "Breach-alarm tactical silhouette."),
            new("8400", "The Birch Knight", "Euclid", "weeping", "skeletal_titan", "bone_snap", 2.55f, 0.0f, 11.8f, W, "White-barked wooden sentinel silent until you turn away."),
            new("8500", "WTH_LUV", "Keter", "cognito", "ocular_swarm", "siren_static", 2.00f, 1.5f, 6.1f, V|L, "Corrupted heart-signal anomaly."),
            new("8600", "The Hallway Archive", "Euclid", "stalker", "tall_lurker", "whisper_choir", 2.20f, 1.4f, 5.9f, V|P, "Filing-cabinet-ribbed archivist cataloguing lost wanderers."),
            new("8888", "Eight-Ball Heist", "Keter", "weeping", "weeping_idol", "clicking", 2.05f, 0.0f, 12.1f, W, "Billiard-masked mannequin crew moving in synchronized freeze-frames."),
            new("8980", "Ergophobia", "Euclid", "cognito", "doppelganger", "sub_drone", 1.95f, 1.3f, 5.6f, L|D, "Fluorescent office-dread phantom draining stamina regen.")
        };

        // Additional 115 authentic numbered SCP Foundation anomalies across Series I-VIII to bring the rotating roster past 325+!
        private static readonly (int num, string title, string cls, string beh, string rig, string audio, byte flags)[] ExtendedSeries =
        {
            (024, "Game Show of Death", "Euclid", "hunter", "doppelganger", "siren_static", V|L),
            (038, "Imperfect Clone", "Safe", "mimic", "doppelganger", "wheezing", V),
            (040, "Evolution's Child", "Euclid", "hunter", "flesh_amalgam", "flesh_squelch", 0),
            (053, "Young Dread Aura", "Euclid", "cognito", "crooked", "whisper_choir", L),
            (060, "Infernal Occult Skeleton", "Keter", "hunter", "skeletal_titan", "bone_snap", L),
            (073, "Cain Reflection", "Euclid", "doorman", "tall_lurker", "metallic_drag", 0),
            (080, "Dark Form", "Euclid", "umbra", "shadow_wraith", "whisper_choir", F|D),
            (089, "Tophet", "Euclid", "stalker", "weeping_idol", "sub_drone", L),
            (094, "Miniature Event Horizon", "Keter", "umbra", "ocular_swarm", "sub_drone", L|D),
            (108, "Extradimensional Nasal Cavity", "Safe", "ambusher", "flesh_amalgam", "wheezing", D),
            (116, "The Brittle Boy", "Euclid", "ambusher", "crooked", "bone_snap", 0),
            (126, "Invisible Friend", "Euclid", "listener", "shadow_wraith", "whisper_choir", V|F),
            (131, "Corrupted Eye Pod", "Safe", "weeping", "ocular_swarm", "clicking", W),
            (140, "Daevite Chronicle Shade", "Keter", "stalker", "tall_lurker", "whisper_choir", P),
            (153, "Drain Worms", "Euclid", "ambusher", "crawler_quad", "flesh_squelch", D),
            (165, "Creeping Rusty Sands", "Keter", "hunter", "arachnid", "clicking", 0),
            (172, "The Gearman", "Euclid", "listener", "weeping_idol", "clicking", 0),
            (180, "Identity-Thieving Hat", "Euclid", "mimic", "doppelganger", "whisper_choir", V),
            (184, "The Architect Drift", "Euclid", "stalker", "doorman", "sub_drone", L|P),
            (191, "Cyborg Waif", "Safe", "shy", "crooked", "siren_static", L),
            (203, "Tortured Iron Soul", "Euclid", "listener", "skeletal_titan", "metallic_drag", L),
            (212, "The Improver", "Safe", "hunter", "arachnid", "metallic_drag", 0),
            (222, "Clone Coffin", "Euclid", "mimic", "doppelganger", "wheezing", V),
            (238, "Building Complex Tumor", "Keter", "ambusher", "flesh_amalgam", "flesh_squelch", D),
            (246, "Prophetic Projector", "Euclid", "cognito", "ocular_swarm", "clicking", L),
            (257, "Professor William Woodsworth's Skull", "Safe", "stalker", "skeletal_titan", "bone_snap", P),
            (268, "Neglect Cap Stalker", "Euclid", "weeping", "tall_lurker", "whisper_choir", W|D),
            (278, "Large Mechanical Spider", "Safe", "hunter", "arachnid", "metallic_drag", 0),
            (287, "Spectral Broadsword", "Safe", "stalker", "shadow_wraith", "metallic_drag", F|P|D),
            (296, "Armed Containment Site-03", "Euclid", "doorman", "weeping_idol", "sub_drone", L),
            (307, "Carnivorous Ivy", "Keter", "ambusher", "arachnid", "flesh_squelch", D),
            (312, "Atmospheric Jellyfish", "Euclid", "ambusher", "ceiling_ambusher", "sub_drone", D),
            (319, "A Curious Device", "Keter", "umbra", "ocular_swarm", "sub_drone", L|D),
            (329, "The Cancer Garden", "Euclid", "hunter", "flesh_amalgam", "wheezing", 0),
            (335, "One Hundred and Fifty Floppies", "Safe", "cognito", "doorman", "radio_numbers", L),
            (346, "Pterodactyl", "Safe", "ambusher", "crawler_quad", "clicking", D),
            (359, "The Hawk", "Euclid", "weeping", "weeping_idol", "metallic_drag", W),
            (367, "Little Dog", "Euclid", "hunter", "crawler_quad", "bone_snap", 0),
            (378, "Brainworm", "Euclid", "mimic", "doppelganger", "clicking", V),
            (389, "Message in a Bottle", "Safe", "cognito", "shadow_wraith", "whisper_choir", D),
            (399, "Atomic Manipulation Ring", "Safe", "stalker", "ocular_swarm", "siren_static", L|P),
            (411, "Ancient Precog", "Euclid", "cognito", "tall_lurker", "whisper_choir", 0),
            (418, "Human Jigsaw", "Safe", "ambusher", "crooked", "flesh_squelch", P),
            (428, "The Crowd", "Euclid", "hunter", "flesh_amalgam", "whisper_choir", V),
            (435, "He-Who-Made-Dark", "Keter", "umbra", "shadow_wraith", "sub_drone", L|F|D),
            (444, "Language of Global Harmony", "Euclid", "mimic", "doppelganger", "whisper_choir", V),
            (452, "Dreamcatcher Spider", "Safe", "ambusher", "arachnid", "clicking", D),
            (466, "Mobile Veins", "Euclid", "ambusher", "flesh_amalgam", "flesh_squelch", D),
            (475, "Soap of a Pope", "Safe", "stalker", "weeping_idol", "whisper_choir", 0),
            (484, "Memory Stealing Drug", "Euclid", "cognito", "shadow_wraith", "sub_drone", L|D),
            (492, "Animated Cloth Dummy", "Safe", "mimic", "doppelganger", "wheezing", V),
            (504, "Velocity Projectile", "Safe", "listener", "ocular_swarm", "bone_snap", 0),
            (511, "Basement Cat", "Euclid", "hunter", "flesh_amalgam", "flesh_squelch", D),
            (529, "Josie the Half-Cat", "Safe", "stalker", "crawler_quad", "whisper_choir", P),
            (542, "Herr Chirurg", "Euclid", "hunter", "crooked", "bone_snap", V),
            (554, "The Perfect Murder", "Euclid", "doorman", "shadow_wraith", "metallic_drag", D),
            (566, "Word a Day", "Safe", "cognito", "tall_lurker", "whisper_choir", V),
            (579, "[DATA EXPUNGED]", "Keter", "shy", "ocular_swarm", "siren_static", L|P|D),
            (593, "Contagious Innumeracy", "Euclid", "cognito", "doppelganger", "radio_numbers", 0),
            (606, "The Teacher", "Euclid", "stalker", "ocular_swarm", "siren_static", L|P),
            (619, "Lucky Jeans Husk", "Safe", "hunter", "doppelganger", "wheezing", 0),
            (631, "Nyctophobic Nocturnal Predator", "Euclid", "umbra", "crawler_quad", "clicking", F|D),
            (644, " scorching Strider", "Euclid", "hunter", "tall_lurker", "siren_static", L),
            (659, "Communal Avian Intelligence", "Euclid", "listener", "crawler_quad", "clicking", V),
            (674, "Exposition Gun", "Safe", "cognito", "doppelganger", "radio_numbers", 0),
            (686, " Infectious Lactation Husk", "Safe", "hunter", "flesh_amalgam", "flesh_squelch", 0),
            (699, "Mystery Box", "Euclid", "doorman", "doorman", "sub_drone", P),
            (706, "Perfect Porcelain Doll", "Euclid", "shy", "weeping_idol", "statue_scrape", W),
            (715, "My Face That I May Be", "Euclid", "mimic", "doppelganger", "whisper_choir", V),
            (732, "Fan-Fic Plague", "Keter", "cognito", "ocular_swarm", "siren_static", L),
            (748, "Industrial Dissolution", "Keter", "hunter", "weeping_idol", "metallic_drag", L),
            (757, "The Fruit Tree", "Safe", "ambusher", "flesh_amalgam", "flesh_squelch", 0),
            (769, "Ancient Encyclopedia", "Safe", "cognito", "tall_lurker", "whisper_choir", 0),
            (776, "The Cult of Youth", "Euclid", "hunter", "doppelganger", "whisper_choir", V),
            (795, "Reality-Bending Cat", "Euclid", "stalker", "crawler_quad", "sub_drone", P),
            (808, "The Mechanical Choir", "Euclid", "listener", "weeping_idol", "metallic_drag", V|L),
            (817, "Random Metamorphism", "Euclid", "hunter", "flesh_amalgam", "bone_snap", 0),
            (833, "Charity Worms", "Euclid", "hunter", "flesh_amalgam", "flesh_squelch", 0),
            (844, "Crybaby", "Keter", "ambusher", "arachnid", "wheezing", V|D),
            (858, "Gravity's Rainbow", "Euclid", "cognito", "ocular_swarm", "sub_drone", L),
            (867, "Blood Spruce", "Euclid", "ambusher", "crooked", "bone_snap", 0),
            (874, "Abyssal Fluid", "Euclid", "stalker", "shadow_wraith", "flesh_squelch", P|D),
            (887, "Hypergraphia", "Safe", "listener", "crooked", "clicking", 0),
            (899, "Lost Children", "Euclid", "umbra", "shadow_wraith", "whisper_choir", V|F|D),
            (912, "Autonomous SWAT Armor", "Safe", "hunter", "doppelganger", "metallic_drag", 0),
            (920, "Mr. Lost", "Euclid", "stalker", "tall_lurker", "whisper_choir", P),
            (938, "Blood and Thunder", "Keter", "hunter", "ocular_swarm", "siren_static", L|P),
            (949, "Wondertainment Land", "Euclid", "mimic", "doppelganger", "radio_numbers", V),
            (962, "Tower of Babble", "Euclid", "hunter", "arachnid", "metallic_drag", V),
            (973, "Smokey", "Euclid", "hunter", "tall_lurker", "siren_static", L),
            (983, "The Birthday Monkey", "Safe", "listener", "crawler_quad", "radio_numbers", V),
            (997, "Vermin Suppressor", "Safe", "cognito", "arachnid", "clicking", D),
            (1026, "Someone You Know", "Euclid", "mimic", "doppelganger", "whisper_choir", V),
            (1050, "Obsidian Obelisk", "Euclid", "doorman", "weeping_idol", "sub_drone", L),
            (1089, "The IC-3", "Euclid", "cognito", "crooked", "wheezing", 0),
            (1143, "Cool Guys", "Euclid", "weeping", "tall_lurker", "siren_static", W|L),
            (1179, "Centralian Fire Demon", "Keter", "hunter", "skeletal_titan", "wheezing", L|D),
            (1206, "Lost Muse", "Euclid", "stalker", "skeletal_titan", "whisper_choir", P),
            (1248, "Personality Recorder", "Safe", "mimic", "doppelganger", "radio_numbers", V),
            (1281, "The Harbinger", "Safe", "listener", "ocular_swarm", "radio_numbers", V),
            (1322, "Glory Hole Breach", "Keter", "ambusher", "arachnid", "clicking", L),
            (1356, "Rubber Duck", "Safe", "stalker", "ocular_swarm", "clicking", P),
            (1399, "Another Way of Hearing", "Safe", "listener", "shadow_wraith", "whisper_choir", V|D),
            (1417, "Swiss Army Banana", "Safe", "ambusher", "crawler_quad", "clicking", 0),
            (1461, "House of the Worm", "Euclid", "hunter", "weeping_idol", "metallic_drag", L),
            (1483, "Third Antarctic Empire", "Euclid", "hunter", "crooked", "clicking", 0),
            (1510, "The Tarnished Legionnaire", "Euclid", "weeping", "weeping_idol", "metallic_drag", W),
            (1548, "The Star, the Hateful", "Keter", "cognito", "ocular_swarm", "radio_numbers", V|L),
            (1590, "The Book of Tamlin", "Safe", "cognito", "shadow_wraith", "siren_static", L),
            (1633, "The Most Dangerous Video Game", "Euclid", "hunter", "crooked", "siren_static", V|L),
            (1670, "Interdimensional Cafe", "Euclid", "doorman", "doorman", "wheezing", P),
            (1718, "Experimental Containment", "Euclid", "stalker", "ocular_swarm", "sub_drone", L|P),
            (1746, "Anomalous Weather", "Keter", "umbra", "shadow_wraith", "siren_static", L|F),
            (1790, "layla Reborn", "Euclid", "mimic", "doppelganger", "whisper_choir", V),
            (1810, "Mr. Pierrot", "Euclid", "hunter", "tall_lurker", "wheezing", V|D),
            (1839, "Fish Reproductive Method", "Euclid", "cognito", "doppelganger", "flesh_squelch", 0),
            (1883, "Gamified App", "Euclid", "stalker", "doppelganger", "radio_numbers", L),
            (1926, "Mewts", "Safe", "listener", "crawler_quad", "whisper_choir", V),
            (1968, "Global Retrocausality Torus", "Keter", "cognito", "weeping_idol", "sub_drone", L|P),
            (1981, "RONALD REAGAN CUT UP WHILE TALKING", "Safe", "mimic", "crooked", "radio_numbers", V|L|D),
            (2019, "Gelatinous Brain Cube", "Euclid", "cognito", "ocular_swarm", "flesh_squelch", 0),
            (2066, "Highly Visible Entity", "Euclid", "shy", "ocular_swarm", "siren_static", L),
            (2099, "Brain in a Jar", "Safe", "listener", "weeping_idol", "radio_numbers", V|L),
            (2146, "The Space Whale", "Euclid", "stalker", "ocular_swarm", "sub_drone", P),
            (2188, "Life and Times", "Euclid", "cognito", "tall_lurker", "whisper_choir", 0),
            (2217, "Hammer and Anvil", "Thaumiel", "hunter", "flesh_amalgam", "metallic_drag", L),
            (2256, "Very Tall Things", "Neutralized", "weeping", "skeletal_titan", "sub_drone", W),
            (2287, "Mr. Headless", "Safe", "cognito", "doppelganger", "wheezing", L),
            (2337, "Dr. Spanko", "Euclid", "listener", "crawler_quad", "siren_static", V),
            (2385, "Somewhere in the Between", "Euclid", "hunter", "flesh_amalgam", "flesh_squelch", D),
            (2406, "The Colossus", "Safe", "weeping", "weeping_idol", "metallic_drag", W),
            (2439, "Slot Zero", "Keter", "cognito", "shadow_wraith", "siren_static", L|D),
            (2501, "The Claw", "Safe", "ambusher", "crooked", "metallic_drag", 0),
            (2547, "Dog Days Of Summer", "Euclid", "hunter", "crawler_quad", "wheezing", V),
            (2599, "Not Good Enough", "Euclid", "hunter", "doppelganger", "bone_snap", L),
            (2639, "Video Game Violence", "Euclid", "hunter", "tall_lurker", "siren_static", L|P),
            (2682, "The Blind Idiot", "Neutralized", "cognito", "ocular_swarm", "sub_drone", L),
            (2700, "Teleforce", "Keter", "stalker", "ocular_swarm", "siren_static", L|P),
            (2747, "As Below, So Above", "Keter", "umbra", "shadow_wraith", "sub_drone", L|F|P|D),
            (2786, "The Archetype", "Keter", "mimic", "doppelganger", "whisper_choir", V|P),
            (2820, "Vaishnavastra", "Keter", "stalker", "ocular_swarm", "siren_static", L),
            (2875, "The Town That Got Fucked By Bears", "Keter", "hunter", "flesh_amalgam", "wheezing", 0),
            (2912, "Clowny Clown Clown", "Euclid", "mimic", "crooked", "wheezing", V),
            (2966, "InfiniTP", "Euclid", "doorman", "flesh_amalgam", "siren_static", L),
            (2999, "The Black Cat and the White Rabbit", "Keter", "hunter", "skeletal_titan", "siren_static", V|L),
            (3001, "Red Reality", "Euclid", "stalker", "shadow_wraith", "sub_drone", V|L|P|D),
            (3007, "World of Two Artists", "Keter", "cognito", "skeletal_titan", "siren_static", L|D),
            (3041, "The Red Knife", "Safe", "ambusher", "shadow_wraith", "metallic_drag", D),
            (3125, "The Escapee", "Keter", "cognito", "ocular_swarm", "siren_static", V|L|P),
            (3200, "Chronos", "Keter", "umbra", "ocular_swarm", "sub_drone", L|F|D),
            (3312, "Ollie", "Euclid", "mimic", "doppelganger", "whisper_choir", V),
            (3456, "The Orcadian Horsemen", "Keter", "hunter", "skeletal_titan", "wheezing", V|L),
            (3515, "Unearth", "Safe", "stalker", "crooked", "flesh_squelch", P|D),
            (3626, "Do Not Stop Reading", "Euclid", "weeping", "skeletal_titan", "bone_snap", W|L),
            (3700, "Tides of War", "Keter", "hunter", "flesh_amalgam", "sub_drone", L),
            (3890, "Forget-Me-Not", "Keter", "stalker", "shadow_wraith", "whisper_choir", V|P|D),
            (3930, "The Pattern Screamer", "Keter", "cognito", "shadow_wraith", "siren_static", V|L|P|D),
            (3935, "This Thing a Quiet Madness Made", "Euclid", "mimic", "crooked", "whisper_choir", V|L|P),
            (3966, "Falling Out", "Euclid", "ambusher", "arachnid", "clicking", F|D),
            (3999, "I Am At The Center", "Apollyon", "cognito", "flesh_amalgam", "siren_static", V|L|P),
            (4000, "Taboo Nameless Fae", "Keter", "mimic", "tall_lurker", "whisper_choir", V),
            (4006, "MassaTruthetts", "Thaumiel", "stalker", "doppelganger", "radio_numbers", P),
            (4182, "There Is No Site-5", "Keter", "doorman", "doorman", "sub_drone", L|P),
            (4205, "In The Eyes of the Beholder", "Keter", "shy", "ocular_swarm", "siren_static", L|D),
            (4335, "A Welt In The Crucible", "Keter", "umbra", "shadow_wraith", "clicking", L|F|D),
            (4485, "Such Black Light", "Keter", "cognito", "weeping_idol", "sub_drone", L|F|D),
            (4511, "SWINE GOD", "Euclid", "hunter", "flesh_amalgam", "metallic_drag", L),
            (4606, "Deimos", "Keter", "hunter", "arachnid", "bone_snap", D),
            (4773, "Stuffed Bear Anomaly", "Euclid", "weeping", "crawler_quad", "whisper_choir", W),
            (4840, "The Demon Lancelot", "Keter", "hunter", "skeletal_titan", "metallic_drag", V|L),
            (4971, "Rituals", "Archon", "cognito", "crooked", "sub_drone", L|D),
            (5000, "Why? Absolute Exclusion Suit", "Safe", "hunter", "doppelganger", "metallic_drag", P),
            (5145, "I Buried The Sun", "Euclid", "umbra", "shadow_wraith", "sub_drone", L|F|D),
            (5999, "This is Where I Died", "Keter", "weeping", "weeping_idol", "whisper_choir", V|W|L|D),
            (6001, "Avalon Echo", "Compelling", "mimic", "doppelganger", "whisper_choir", V),
            (6666, "The Demon Hector and the Dread Titania", "Keter", "hunter", "skeletal_titan", "sub_drone", V|L|D),
            (7004, "Insane, Wailing, Feral", "Keter", "listener", "flesh_amalgam", "whisper_choir", V|L|D),
            (7179, "E is for Eternity", "Safe", "stalker", "tall_lurker", "clicking", P),
            (7816, "Cabinet Butcher", "Euclid", "ambusher", "crooked", "bone_snap", D),
            (8999, "Feed the Trolls", "Keter", "hunter", "flesh_amalgam", "wheezing", V|D)
        };

        public static List<EntityDef> BuildAll()
        {
            var list = new List<EntityDef>(Entries.Length + ExtendedSeries.Length);
            var seen = new HashSet<string>();

            foreach (var r in Entries)
            {
                string id = "scp-" + r.num.ToLowerInvariant();
                if (!seen.Add(id)) continue;
                list.Add(CreateDef(id, "SCP-" + r.num, r.title, r.cls, r.beh, r.rig, r.audio, r.h, r.walk, r.run, r.flags, r.desc, r.num));
            }

            foreach (var e in ExtendedSeries)
            {
                string numStr = e.num.ToString("000");
                string id = "scp-" + numStr;
                if (!seen.Add(id)) continue;
                float h = e.rig == "crawler_quad" || e.rig == "arachnid" ? 1.45f : e.rig == "skeletal_titan" ? 2.55f : 2.05f;
                float walk = e.beh == "weeping" ? 0f : 1.4f;
                float run = e.beh == "weeping" ? 11.8f : 6.0f;
                string desc = $"{e.cls}-class {e.beh} anomaly ({e.title}). Part of the rotating Backrooms containment breach roster.";
                list.Add(CreateDef(id, "SCP-" + numStr, e.title, e.cls, e.beh, e.rig, e.audio, h, walk, run, e.flags, desc, numStr));
            }

            return list;
        }

        private static EntityDef CreateDef(string id, string name, string title, string cls, string beh, string rig, string audio,
            float height, float walk, float run, byte flags, string desc, string wikiSlug)
        {
            uint hash = Hash.U(id.GetHashCode(), 41, 97);
            PaletteFor(rig, beh, hash, out Color pri, out Color acc, out Color eye, out float glow);

            string cleanSlug = wikiSlug.Split('-')[0];
            if (int.TryParse(cleanSlug, out int n) && n < 100) cleanSlug = n.ToString("000");

            return new EntityDef
            {
                id = id,
                name = name,
                nickname = title,
                objectClass = cls,
                description = desc,
                behaviour = beh,
                model = "", // procedural nightmare rig unless overridden by StreamingAssets/Entities/<id>/model.glb
                rigArchetype = rig,
                primaryColor = pri,
                accentColor = acc,
                eyeColor = eye,
                eyeGlow = glow,
                height = height,
                walkSpeed = walk,
                runSpeed = run,
                turnSpeed = beh == "weeping" ? 720f : 320f,
                sightRange = beh == "listener" ? 0f : beh == "weeping" ? 32f : 19f,
                sightAngle = beh == "weeping" ? 360f : 135f,
                hearing = beh == "listener" ? 1.35f : (flags & V) != 0 ? 1.15f : 0.9f,
                attackRange = Mathf.Clamp(height * 0.68f, 1.35f, 2.0f),
                killDelay = beh == "weeping" ? 0.15f : 0.55f,
                triggerTime = 3.8f,
                phaseThroughWalls = (flags & P) != 0,
                loseInterestTime = 18f,
                lifetime = 210f,
                prefersDark = (flags & D) != 0,
                stealsVoices = (flags & V) != 0 || beh == "mimic" || beh == "listener",
                flickersLights = (flags & L) != 0 || beh == "doorman",
                freezesWhenWatched = (flags & W) != 0 || beh == "weeping",
                fearsFlashlight = (flags & F) != 0 || beh == "umbra",
                audioProfile = audio,
                levels = new[] { "level0", "level1", "level2" },
                spawnWeight = 1f,
                maxAlive = 1,
                minSpawnDistance = 18f,
                maxSpawnDistance = 36f,
                sounds = new SoundSet { volume = 0.95f },
                credit = new Credit
                {
                    article = $"{name} - {title}",
                    author = "SCP Wiki Community",
                    articleUrl = "https://scp-wiki.wikidot.com/scp-" + cleanSlug.ToLowerInvariant(),
                    assets = "Procedural 3D horror rig & synthesised audio (Backrooms Escape)",
                    license = "CC BY-SA 3.0",
                    licenseUrl = "https://creativecommons.org/licenses/by-sa/3.0/",
                    changes = "Adapted into a procedural Backrooms entity with custom horror AI & audio."
                }
            };
        }

        private static void PaletteFor(string rig, string beh, uint hash, out Color pri, out Color acc, out Color eye, out float glow)
        {
            float jitter = ((hash & 0xFF) / 255f - 0.5f) * 0.06f;
            switch (rig)
            {
                case "shadow_wraith":
                    pri = new Color(0.03f, 0.03f, 0.04f);
                    acc = new Color(0.08f, 0.07f, 0.09f);
                    eye = beh == "umbra" ? new Color(1.0f, 0.95f, 0.85f) : new Color(0.9f, 0.15f, 0.15f);
                    glow = 3.5f;
                    break;
                case "flesh_amalgam":
                    pri = new Color(0.46f + jitter, 0.14f, 0.13f);
                    acc = new Color(0.68f, 0.24f + jitter, 0.20f);
                    eye = new Color(0.95f, 0.88f, 0.45f);
                    glow = 1.8f;
                    break;
                case "weeping_idol":
                    pri = new Color(0.62f + jitter, 0.59f + jitter, 0.54f);
                    acc = new Color(0.28f, 0.14f, 0.10f);
                    eye = new Color(0.1f, 0.1f, 0.1f);
                    glow = 0f;
                    break;
                case "doppelganger":
                    pri = new Color(0.72f + jitter, 0.62f + jitter, 0.18f); // uncanny tarnished Backrooms hazmat yellow
                    acc = new Color(0.22f, 0.20f, 0.16f);
                    eye = new Color(0.85f, 0.95f, 1.0f);
                    glow = 2.2f;
                    break;
                case "skeletal_titan":
                    pri = new Color(0.76f + jitter, 0.72f + jitter, 0.63f);
                    acc = new Color(0.32f, 0.12f, 0.10f);
                    eye = new Color(1.0f, 0.35f, 0.15f);
                    glow = 2.8f;
                    break;
                case "doorman":
                    pri = new Color(0.34f + jitter, 0.16f, 0.12f);
                    acc = new Color(0.18f, 0.09f, 0.08f);
                    eye = new Color(0.95f, 0.9f, 0.8f);
                    glow = 0.8f;
                    break;
                default:
                    pri = new Color(0.22f + jitter, 0.20f + jitter, 0.19f);
                    acc = new Color(0.45f, 0.12f, 0.11f);
                    eye = new Color(0.95f, 0.85f, 0.65f);
                    glow = 2.4f;
                    break;
            }
        }
    }
}
