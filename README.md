# Real-Time Decision Making in Video Games

Projet réalisé dans le cadre du module **Real Time Decision Making in Video Game** à l'EPF — année universitaire 2026–2027.

Le projet consiste à améliorer l'intelligence artificielle de l'armée **RED** dans un jeu de stratégie en temps réel développé avec **Unity 3D**, **C#** et **Behavior Designer**.

L'objectif principal est de permettre à l'armée RED, pourtant en infériorité numérique, de combattre efficacement l'armée GREEN contrôlée par l'IA d'origine.

---

## 1. Contexte

Au début d'une bataille :

| Unité | RED | GREEN |
|---|---:|---:|
| Drones | 13 | 19 |
| Tourelles d'artillerie | 6 | 9 |

L'armée RED doit donc compenser son désavantage numérique grâce à une meilleure prise de décision, une meilleure coordination et une utilisation plus efficace de ses unités.

### Contraintes respectées

- Le comportement de l'armée GREEN n'a pas été modifié.
- Les caractéristiques imposées des unités existantes ont été conservées.
- Les missiles des drones restent des projectiles à trajectoire rectiligne.
- Les améliorations portent principalement sur la stratégie, la sélection des cibles, le déplacement et la coordination.
- Une fonctionnalité différenciante a été ajoutée : le **Medic RED**.

---

## 2. Stratégie générale de l'armée RED

Notre stratégie repose principalement sur :

- la sélection dynamique des cibles ;
- la coordination entre drones et artillerie ;
- la réduction du gaspillage de tirs ;
- la prise en compte de l'accessibilité des cibles ;
- la gestion des projectiles déjà en vol ;
- l'adaptation aux changements du champ de bataille.

L'objectif est d'éviter que plusieurs unités RED utilisent inutilement leurs ressources sur la même cible.

---

## 3. Drones RED

Les drones rouges utilisent un Behavior Tree spécifique comportant plusieurs comportements tactiques.

Ils peuvent notamment :

- sélectionner dynamiquement une cible ;
- se déplacer avec `NavMesh` ;
- maintenir une distance adaptée à la cible ;
- s'orienter avant le tir ;
- tirer plusieurs missiles de manière convergente ;
- effectuer des mouvements de repositionnement ;
- éviter certaines menaces d'artillerie ;
- limiter le nombre de drones concentrés sur une même tourelle.

### Raiders

Au début de chaque bataille, **4 drones RED** sont affectés au rôle de Raiders.

Les Raiders donnent principalement la priorité aux tourelles GREEN accessibles aux drones.

Les autres drones peuvent continuer à attaquer les drones ennemis et à participer au combat général.

Cette répartition permet d'éviter que toute l'armée RED applique exactement la même stratégie.

---

## 4. Artillerie RED

Les tourelles RED disposent d'une sélection tactique des cibles.

Les principaux critères comprennent :

- le type de cible ;
- les points de vie restants ;
- la distance ;
- les dégâts des projectiles déjà en vol ;
- la mobilité des drones ennemis ;
- le regroupement de plusieurs drones ;
- l'accessibilité des tourelles ennemies pour les drones RED.

L'artillerie donne notamment la priorité aux tourelles GREEN difficiles d'accès pour les drones.

> L'artillerie rouge donne la priorité aux tourelles difficiles d'accès pour les drones, tandis que les drones attaquent les tourelles accessibles.

---

## 5. Réduction du overkill

Une partie importante de la stratégie consiste à éviter que plusieurs unités RED tirent inutilement sur une cible déjà condamnée.

Les dégâts des projectiles en vol sont enregistrés afin d'estimer la santé future de la cible :

```text
Santé estimée =
Santé actuelle
- dégâts des projectiles déjà en vol
```

Lorsque les dégâts prévus sont suffisants pour détruire une cible, les autres unités peuvent sélectionner une nouvelle cible.

Cette logique permet :

- de mieux répartir les tirs ;
- d'éviter le gaspillage de missiles et de grenades ;
- d'améliorer l'efficacité globale de l'armée RED.

---

## 6. Coordination drones / artillerie

La coopération entre les drones et les tourelles RED repose également sur l'analyse du `NavMesh`.

Le système évalue si une tourelle GREEN est facilement accessible aux drones.

La stratégie peut alors répartir les tâches :

- **tourelle accessible** → cible adaptée aux drones / Raiders ;
- **tourelle difficile d'accès** → priorité donnée à l'artillerie.

Cette logique permet d'exploiter plus efficacement les capacités spécifiques de chaque type d'unité.

---

## 7. Medic RED

Une nouvelle unité de support mobile a été ajoutée comme fonctionnalité différenciante.

Le **Medic RED** ne possède pas d'arme.

Son objectif est de maintenir les unités RED en vie pendant le combat.

### Fonctionnement

Le Medic :

- recherche automatiquement les unités RED blessées ;
- donne la priorité aux drones ;
- peut également soigner les tourelles ;
- vérifie si la cible est accessible ;
- se déplace avec `NavMeshAgent` ;
- s'arrête lorsqu'il arrive à portée ;
- soigne progressivement sa cible ;
- change de cible lorsque celle-ci est complètement soignée ou détruite ;
- abandonne temporairement les cibles bloquées ou inaccessibles ;
- reste inactif lorsqu'aucun soin n'est nécessaire.

Le système de soin utilise directement le composant `Health` commun aux unités.

---

## 8. Navigation du Medic

Le script :

```text
Assets/Scripts/MedicNavigation.cs
```

permet au Medic de rechercher différentes positions accessibles autour de sa cible.

Cette logique est particulièrement utile pour les tourelles situées à proximité d'obstacles ou de zones non directement accessibles.

Le Medic possède également un mécanisme permettant de détecter lorsqu'il ne progresse plus vers sa cible.

Dans ce cas, il peut abandonner temporairement cette cible et rechercher un autre allié blessé.

---

## 9. Apparence du Medic

Le Medic est représenté par un robot médical RED construit à partir de primitives Unity.

Il possède notamment :

- une identité visuelle rouge et blanche ;
- des symboles médicaux ;
- une barre de vie identique à celle des drones RED ;
- un effet lumineux pendant les soins ;
- une ligne reliant le Medic à sa cible ;
- un cercle de soin.

Le Medic possède également ses propres points de vie et peut être endommagé ou détruit pendant la bataille.

---

## 10. Outils Editor

Un outil Unity Editor permet de créer ou mettre à jour automatiquement le prefab du Medic :

```text
Tools > RED AI > Create Medic Prefab
```

Le prefab est enregistré dans :

```text
Assets/Prefabs/MedicRed.prefab
```

Les matériaux associés sont stockés dans :

```text
Assets/Materials/MedicRed/
```

Des outils de test sont également disponibles en mode Play :

```text
Tools > RED AI > Test > Injure Selected RED Units
```

et :

```text
Tools > RED AI > Test > Defeat Selected RED Units
```

Ils permettent de tester rapidement le comportement du Medic.

---

## 11. Gestion de la fin de bataille

Le Medic ne doit pas empêcher la détection normale d'une victoire ou d'une défaite.

Lorsque tous les drones et toutes les tourelles RED sont détruits, les éventuels Medics restants ne bloquent pas la fin de la bataille.

Après l'affichage du résultat, le Medic :

- arrête son déplacement ;
- arrête de rechercher des cibles ;
- arrête les soins ;
- désactive ses effets visuels.

---

## 12. Restart et réinitialisation tactique

Certaines informations tactiques sont conservées dans des variables statiques pendant une bataille.

Avant de recharger la scène, ces informations sont maintenant réinitialisées.

Sont notamment réinitialisés :

- les Raiders ;
- les cibles des drones ;
- les réservations de cibles ;
- les missiles en attente ;
- les unités mises en cache ;
- les informations de menace ;
- les données d'accessibilité des tourelles.

Une nouvelle bataille démarre ainsi dans un état propre.

Les **4 Raiders** sont automatiquement attribués à de nouvelles instances de drones après un Restart.

---

## 13. Statistiques de combat

Le projet intègre également un système global de statistiques :

```text
BattleStatsManager.cs
```

Il permet notamment de mesurer :

### Drones

- nombre de missiles tirés ;
- nombre de missiles ayant touché une cible ;
- taux de précision.

### Artillerie

- nombre de tirs ;
- nombre de tirs ayant touché au moins une unité ;
- taux de précision ;
- nombre total d'unités touchées ;
- nombre moyen d'unités touchées par tir.

### Fin de bataille

Le panneau final indique notamment :

- l'armée gagnante ;
- la durée de la bataille ;
- le nombre de drones survivants ;
- le nombre de tourelles survivantes ;
- les points de vie cumulés restants.

Le Medic est une fonctionnalité supplémentaire et ne modifie pas le format principal demandé pour les statistiques finales.

---

## 14. Fichiers principaux

### IA RED

```text
Assets/Scripts/Army/ArmyManagerRed.cs
Assets/Scripts/Army/RedDroneTactics.cs
Assets/Scripts/MyBehaviorTrees/DroneRedBehavior.asset
Assets/Scripts/MyBehaviorTrees/TurretRedBehavior.asset
```

### Medic

```text
Assets/Scripts/Medic.cs
Assets/Scripts/MedicNavigation.cs
Assets/Scripts/SelectRedMedicTarget.cs
Assets/Prefabs/MedicRed.prefab
Assets/Materials/MedicRed/
```

### Statistiques et gestion du combat

```text
Assets/Scripts/Army/BattleStatsManager.cs
Assets/Scripts/Army/Health.cs
Assets/Scripts/Restart.cs
```

### Outils Editor

```text
Assets/Editor/RedMedicPrefabCreator.cs
Assets/Editor/RedMedicTestTools.cs
```

---

## 15. Répartition du travail

Le projet est réalisé par une équipe de trois étudiants.

| Membre | Responsabilités principales |
|---|---|
| **LIU Yao** | `ArmyManagerRed`, stratégie globale, allocation des cibles et coordination |
| **Ruofan** | `DroneRedBehavior`, logique de combat et déplacement des drones RED |
| **CHENG Zihao** | `TurretRedBehavior`, artillerie RED, réduction du overkill, statistiques, intégration et développement du Medic RED |

L'intégration, les tests et le débogage sont réalisés collectivement.

---

## 16. État actuel

### Implémenté

- IA spécifique des drones RED ;
- stratégie Raiders ;
- IA spécifique de l'artillerie RED ;
- sélection tactique des cibles ;
- coordination drones / artillerie ;
- analyse de l'accessibilité des tourelles ;
- réduction du overkill ;
- gestion des projectiles déjà en vol ;
- statistiques globales de combat ;
- panneau de résultat ;
- Medic RED autonome ;
- navigation du Medic ;
- soin des drones et tourelles ;
- barre de vie et effets visuels du Medic ;
- arrêt automatique du Medic après la bataille ;
- gestion correcte de la défaite lorsque seul le Medic reste ;
- reset tactique lors du Restart.

### Reste à réaliser pour la livraison finale

- effectuer les **20 batailles valides** ;
- enregistrer les résultats dans le fichier CSV demandé ;
- analyser les statistiques finales ;
- préparer les Release Notes ;
- enregistrer la vidéo de démonstration ;
- préparer l'archive Unity finale sans le dossier `Library`.

---

## 17. Technologies utilisées

- **Unity 3D**
- **C#**
- **Behavior Designer**
- **NavMesh**
- **Unity UI**
- **TextMeshPro**
- **Git / GitHub**

---

## 18. Conclusion

L'armée RED dispose désormais d'une stratégie plus structurée fondée sur la coordination entre drones, Raiders et artillerie.

Les drones et l'artillerie ne ciblent plus les ennemis de manière totalement indépendante : l'accessibilité des tourelles, les dégâts déjà en vol et la situation du combat sont utilisés pour répartir plus efficacement les attaques.

L'ajout du Medic RED apporte également une nouvelle dimension de support et constitue la fonctionnalité différenciante du projet.

La prochaine étape consiste principalement à effectuer les 20 batailles finales et à analyser les résultats obtenus.
