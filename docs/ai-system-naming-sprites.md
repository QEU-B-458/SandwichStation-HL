# AI System — Names & Sprites Checklist

All player-facing names and sprites in the AI system. Update each row with the new name/sprite when ready.

---

## Entity Names

| File | ID | Current Name | New Name |
|------|----|-------------|----------|
| `_Sandwich/Entities/Mobs/Cyborgs/ai_deployment_borg.yml` | BorgChassisAiDeployment | AI deployment borg | |
| `_Sandwich/Entities/Mobs/Cyborgs/ai_deployment_borg.yml` | PlayerBorgAiDeployment | (inherits) | |
| `_Sandwich/Entities/Markers/Spawners/ai_deployment.yml` | SpawnPointAiDeploymentBorg | AI deployment borg | |
| `_Sandwich/Body/Prototypes/ai_deployment_borg.yml` | AiDeploymentBorg | AI Deployment Borg | |
| `_Sandwich/Entities/Objects/Devices/ai_auth_module.yml` | AiAuthModule | AI auth module | |
| `_Sandwich/Entities/Objects/Devices/ai_auth_module.yml` | AiAuthModuleStation | AI auth module | |
| `_Sandwich/Entities/Objects/Devices/ai_auth_module.yml` | AiAuthModulePlayer | AI auth module | |
| `_Sandwich/Entities/Objects/Devices/ai_boris_module.yml` | AiBorisModule | AI cyborg remote control module | |
| `_Sandwich/Entities/Objects/Devices/ai_modules.yml` | AiCargoModule | AI cargo module | |
| `_Sandwich/Entities/Objects/Devices/ai_modules.yml` | AiSecurityModule | AI security module | |
| `_Sandwich/Entities/Objects/Devices/ai_modules.yml` | AiPowerModule | AI power management module | |
| `_Sandwich/Entities/Objects/Devices/ai_modules.yml` | AiAtmosModule | AI atmospherics module | |
| `_Sandwich/Entities/Objects/Devices/ai_id_cards.yml` | AiStationIdCard | AI station ID card | |
| `_Sandwich/Entities/Objects/Devices/ai_id_cards.yml` | AiDeploymentBorgIdCard | AI deployment borg ID card | |
| `_Sandwich/Entities/Structures/ai_interface.yml` | AiInterface | AI interface | |
| `_Sandwich/Entities/Structures/Machines/ai_network.yml` | AiControllerServer | AI controller server | |
| `_Sandwich/Entities/Structures/Machines/ai_network.yml` | AiNetworkRelay | AI network relay | |
| `_Sandwich/Entities/Objects/Devices/Circuitboards/ai_network.yml` | AiControllerServerBoard | AI controller server machine board | |
| `_Sandwich/Entities/Objects/Devices/Circuitboards/ai_network.yml` | AiRelayBoard | AI relay machine board | |

---

## Job Roles

| Key | Current Value | New Value |
|-----|--------------|-----------|
| `job-name-ai-deployment-borg` | AI Deployment Borg | |
| `job-description-ai-deployment-borg` | A specialized transport borg. Walk to an AI Interface and upload yourself into an AI core. | |
| `job-supervisors-ai-deployment-borg` | the research director | |
| `job-name-station-ai` | Station AI | |
| `job-description-station-ai` | Follow your laws, serve the crew. | |

---

## Actions

| Action ID | Current Name | Current Description | New Name | New Description |
|-----------|-------------|-------------------|----------|-----------------|
| ActionOpenAiNetwork | AI Network | View connected relays and jump to their grids. | | |
| ActionOpenAiServerPanel | Server Panel | Open the AI Controller Server interface to manage modules and authentication. | | |
| ActionOpenBorisControl | B.O.R.I.S. Control | View paired borgs and transfer your mind into one. | | |
| ActionBorisReturnToCore | Return to AI Core | Transfer your mind back to the AI core. | | |
| ActionAiDeploymentToggleIdCard | Toggle ID Card | Eject your ID card to hand, or store it back in your internal slot. | | |
| ActionAiDeploymentUpload | Upload to AI Core | Upload your mind into the linked AI core. Your borg will go inert. | | |
| ActionAiDeploymentShunt | Return to Deployment Borg | Transfer your mind back to the deployment borg at the AI Interface. | | |

---

## Popup / UI Text

### ai-deployment.ftl
| Key | Current Text | New Text |
|-----|-------------|----------|
| ai-deployment-borg-id-slot | ID Card | |
| ai-deployment-not-in-interface | You need to be inside an AI Interface to upload. | |
| ai-deployment-no-server | The AI Interface is not linked to a server. | |
| ai-deployment-no-core | No AI core is connected to the server. | |
| ai-deployment-core-occupied | The AI core already has an occupant. | |
| ai-deployment-core-error | Something went wrong with the AI core. | |
| ai-deployment-no-mind | No mind detected. | |
| ai-deployment-upload-success | Mind upload complete. Welcome to the core. | |
| ai-deployment-no-borg | No deployment borg to return to. | |
| ai-deployment-borg-destroyed | Your deployment borg has been destroyed. | |
| ai-deployment-shunt-success | Mind transfer complete. You're back in the borg. | |
| ai-deployment-id-ejected | ID card ejected to hand. | |
| ai-deployment-id-stored | ID card stored. | |
| ai-deployment-hands-full | Your hand is full. | |
| ai-deployment-no-id-in-hand | You're not holding anything. | |
| ai-deployment-not-id-card | That doesn't fit in the card slot. | |
| ai-deployment-borg | AI Deployment Borg | |
| petting-success-deploymentborg | You pet the deployment borg. It beeps contentedly. | |
| petting-failure-deploymentborg | The deployment borg buzzes disapprovingly. | |

### ai-auth-module.ftl
| Key | Current Text | New Text |
|-----|-------------|----------|
| ai-auth-module-name | AI auth module | |
| ai-auth-module-desc | Provides an AI with access permissions via an ID card and radio channels via encryption keys. | |
| ai-auth-no-access | Access denied — no valid ID card in auth module. | |
| ai-auth-no-module | No auth module installed. | |
| ai-auth-id-card-label | ID Card | |
| ai-auth-keys-label | Encryption Keys | |
| ai-auth-no-id-card | No ID card inserted. | |
| ai-auth-no-keys | No encryption keys inserted. | |
| ai-auth-id-slot-full | ID card slot is already occupied. | |
| ai-auth-key-slots-full | All encryption key slots are full. | |
| ai-server-ui-tab-modules | Modules | |
| ai-server-ui-tab-auth | Auth | |

### ai-server-modules.ftl
| Key | Current Text | New Text |
|-----|-------------|----------|
| ai-server-panel-closed | The server's panel must be open first. | |
| ai-server-module-full | The server's module slots are full. | |
| ai-server-module-incompatible | This module is not compatible with this server. | |
| ai-server-module-duplicate | A module of this type is already installed. | |
| ai-server-no-power | The server has no power. | |
| ai-server-module-installed | Module installed successfully. | |
| ai-server-module-removed | Module removed. | |
| ai-server-module-required | Required server module not installed. | |
| ai-server-no-linked-server | No server linked to this core. | |
| ai-server-ui-title | AI Controller Server | |
| ai-server-ui-linked-core | Linked Core: | |
| ai-server-ui-no-core | No core linked | |
| ai-server-ui-modules-label | Installed Modules | |
| ai-server-ui-module-counter | Modules: {$actual}/{$max} | |
| ai-server-ui-name-placeholder | Server name... | |

### boris.ftl
| Key | Current Text | New Text |
|-----|-------------|----------|
| boris-pairing-code-label | Pairing Code | |
| boris-paired-borgs-label | Paired Borgs | |
| boris-no-paired-borgs | No borgs paired. | |
| boris-pairing-title | AI Remote Control Pairing | |
| boris-enter-code-label | Enter 4-digit pairing code: | |
| boris-submit-button | Pair | |
| boris-unpair-button | Unpair | |
| boris-paired-to | Paired to: {$server} | |
| boris-enter-code-verb | Enter Pairing Code | |
| boris-invalid-code | Invalid pairing code. | |
| boris-paired | Borg paired to AI server. | |
| boris-unpaired | Borg unpaired from AI server. | |
| boris-borg-locked | Unlock the borg first. | |

---

## Sprites

| Item | Current Sprite | New Sprite |
|------|---------------|------------|
| Deployment borg chassis | `Mobs/Silicon/chassis.rsi` state `peace` | |
| Deployment borg eyes | `peace_e` / `peace_e_r` | |
| Deployment borg light | `peace_l` | |
| ID card (deployment) | `Objects/Misc/id_cards.rsi` state `default` + `idroboticist` | |
| ID card (station AI) | `Objects/Misc/id_cards.rsi` state `gold` + `idcentcom` | |
| Toggle ID Card action | `Objects/Misc/id_cards.rsi` state `default` | |
| Upload to Core action | `Interface/Actions/actions_ai.rsi` state `ai_core` | |
| Return to Borg action | `Interface/Actions/actions_ai.rsi` state `borg_control` | |
| AI Network action | `Interface/Actions/actions_ai.rsi` state `borg_control` | |
| Server Panel action | `Interface/Actions/actions_ai.rsi` state `ai_core` | |
| B.O.R.I.S. Control action | `Interface/Actions/actions_ai.rsi` state `borg_control` | |
| Return to Core action | `Interface/Actions/actions_ai.rsi` state `ai_core` | |
| Job icon in lobby | `JobIconBorg` (generic) | |
| BorgTransponder sprite | `Mobs/Silicon/chassis.rsi` state `peace` | |
