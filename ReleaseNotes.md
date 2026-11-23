# Release Notes — Real-Time Decision Making in Video Games

## 1. Équipe

Projet réalisé dans le cadre du module **Real Time Decision Making in Video Game** à l'EPF — année universitaire 2026–2027.

Membres de l'équipe :

- **LIU Yao**
- **Ruofan**
- **CHENG Zihao**

---

## 2. Réflexion stratégique et tactique

L'armée RED commence chaque bataille avec moins d'unités que l'armée GREEN. Notre stratégie consiste donc à compenser ce désavantage numérique par une meilleure coordination et une sélection plus efficace des cibles.

Les principaux choix tactiques sont les suivants :

- répartition des rôles entre drones et artillerie ;
- concentration des tirs afin de réduire la dispersion des dégâts ;
- prise en compte des projectiles déjà en vol afin de limiter le **overkill** ;
- utilisation de l'accessibilité NavMesh pour répartir les tourelles GREEN entre drones et artillerie ;
- utilisation de **4 Raiders**, chargés principalement d'attaquer les tourelles GREEN accessibles ;
- priorité de l'artillerie RED aux tourelles difficiles d'accès pour les drones ;
- repositionnement et esquive des drones afin de réduire leur vulnérabilité aux dégâts de zone.

L'objectif général est d'utiliser chaque type d'unité sur les cibles pour lesquelles il est le plus efficace.

---

## 3. Fonctionnalités développées

### IA des drones RED

Le comportement des drones RED a été remplacé par une logique spécifique comprenant notamment :

- sélection dynamique des cibles ;
- stratégie Raiders ;
- focus fire ;
- réduction du overkill ;
- recherche d'une position de tir adaptée ;
- tir convergent ;
- strafe pendant la recharge ;
- détection des menaces d'artillerie ;
- esquive des zones dangereuses ;
- détection des chemins incomplets ou des situations de blocage.

### IA des tourelles RED

L'artillerie RED dispose désormais d'une sélection tactique des cibles prenant notamment en compte :

- le type de cible ;
- les points de vie restants ;
- les projectiles déjà en vol ;
- la mobilité des drones ennemis ;
- le regroupement des unités ;
- l'accessibilité des tourelles GREEN pour les drones RED.

L'artillerie donne en priorité ses tirs aux tourelles que les drones ont davantage de difficulté à atteindre.

### Coordination globale

La coordination RED permet notamment :

- de répartir les cibles entre drones et tourelles ;
- d'estimer les dégâts déjà en route vers une cible ;
- d'éviter certains tirs redondants ;
- de gérer les rôles des Raiders ;
- de réinitialiser les données tactiques lors d'un Restart.

---

## 4. Fonctionnalité différenciante : Medic RED

Une nouvelle unité de soutien mobile a été développée pour l'armée RED.

Le Medic :

- détecte automatiquement les unités RED blessées ;
- donne la priorité aux drones ;
- peut également soigner les tourelles ;
- utilise le NavMesh pour rejoindre sa cible ;
- recherche une position de soin accessible ;
- soigne progressivement sans dépasser les points de vie maximum ;
- abandonne temporairement une cible inaccessible ou bloquée ;
- change de cible lorsque celle-ci est détruite ou complètement soignée ;
- arrête son déplacement et ses soins à la fin du combat.

Le Medic possède également une barre de vie et des effets visuels permettant d'identifier clairement une action de soin.

Son propre nombre de points de vie n'est pas ajouté aux statistiques finales demandées pour les drones et les tourelles.

---

## 5. Behavior Trees

### DroneRedBehavior

Structure principale :

```text
Selector
├── Esquive grenade
│   ├── IsGrenadeThreatened
│   └── RedDroneEvadeGrenade
└── Attaque
    ├── SelectRedDroneTarget
    ├── RedDroneEngage
    ├── RedDroneAim
    ├── RedDroneShoot
    └── Recharge / Strafe
```

Les principaux nouveaux comportements sont :

- `SelectRedDroneTarget`
- `RedDroneEngage`
- `RedDroneAim`
- `RedDroneShoot`
- `RedDroneStrafe`
- `IsGrenadeThreatened`
- `RedDroneEvadeGrenade`

### TurretRedBehavior

Le Behavior Tree des tourelles RED utilise notamment :

- `SelectRedTurretTarget`
- logique d'orientation vers la cible ;
- `RedTurretShoot`
- temporisation entre les tirs.

La sélection de cible s'appuie sur la logique tactique RED afin de réduire les tirs inutiles et de mieux coordonner les tourelles avec les drones.

---

## 6. Principaux scripts C#

Principaux scripts ajoutés ou modifiés :

- `ArmyManagerRed.cs`
- `RedDroneTactics.cs`
- `BattleStatsManager.cs`
- `Drone.cs`
- `Health.cs`
- `Medic.cs`
- `MedicNavigation.cs`
- `SelectRedMedicTarget.cs`
- `Restart.cs`
- `SelectRedDroneTarget.cs`
- `RedDroneEngage.cs`
- `RedDroneAim.cs`
- `RedDroneShoot.cs`
- `RedDroneStrafe.cs`
- `IsGrenadeThreatened.cs`
- `RedDroneEvadeGrenade.cs`
- `SelectRedTurretTarget.cs`
- `RedTurretShoot.cs`

Des outils Unity Editor ont également été ajoutés pour générer le prefab du Medic et faciliter ses tests.

---

## 7. Statistiques et validation

`BattleStatsManager` permet de mesurer notamment :

- le gagnant ;
- la durée du combat ;
- le nombre de drones survivants ;
- le nombre de tourelles survivantes ;
- le total des points de vie restants ;
- le nombre de tirs des drones et de l'artillerie ;
- leur précision ;
- l'efficacité des dégâts de zone de l'artillerie.

Pour le calcul final des points de vie restants, seules les unités **Drone** et **Turret** sont prises en compte. Le Medic peut augmenter la santé restante en soignant ces unités, mais ses propres points de vie ne sont pas ajoutés au total final.

Les résultats des **20 batailles valides** seront fournis dans le fichier statistique séparé demandé pour la livraison.

---

## 8. Limitations, bugs connus et pistes d'amélioration

### Limitations

- Le Medic ne soigne qu'une unité à la fois.
- Son efficacité dépend de l'existence d'un chemin NavMesh valide vers une position proche de la cible.
- La prédiction du mouvement des drones ennemis reste une estimation et peut être moins précise lorsqu'une cible change brusquement de direction.
- Certains paramètres tactiques restent configurables et pourraient être ajustés après analyse d'un plus grand nombre de simulations.

### Bugs connus

Aucun bug majeur connu n'a été identifié lors des derniers tests.

Des situations exceptionnelles de **deadlock** peuvent cependant apparaître pendant une simulation. Conformément aux consignes du sujet, ces batailles ne doivent pas être comptabilisées et doivent être relancées.

### Pistes d'amélioration

- adaptation dynamique du nombre de Raiders ;
- amélioration de la prédiction des mouvements ennemis ;
- meilleure évaluation du danger avant l'envoi du Medic ;
- gestion plus avancée des priorités de soin ;
- ajustement dynamique des choix tactiques en fonction de l'évolution du combat.

---

## 9. Répartition du travail

### LIU Yao

- `ArmyManagerRed`
- coordination globale de l'armée RED ;
- allocation des cibles ;
- gestion de la stratégie globale ;
- intégration des différentes logiques tactiques.

### Ruofan

- `DroneRedBehavior`
- stratégie Raiders ;
- sélection des cibles des drones ;
- positionnement tactique ;
- tir convergent ;
- strafe ;
- détection et esquive des grenades ;
- gestion des blocages et des déplacements.

### CHENG Zihao

- `TurretRedBehavior`
- stratégie et sélection des cibles d'artillerie ;
- réduction du overkill ;
- instrumentation des combats ;
- `BattleStatsManager` et interface de résultats ;
- intégration et développement du Medic RED ;
- navigation et logique de soin ;
- effets visuels et barre de vie du Medic ;
- gestion des cas de fin de combat liés au Medic ;
- correction et validation du Restart.

L'intégration, les tests et le débogage final ont été réalisés collectivement par l'équipe.