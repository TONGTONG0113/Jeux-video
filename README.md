# RTS War Game — RED vs GREEN

Projet réalisé dans le cadre du module **Real Time Decision Making in Video Game** à l’EPF.

Le projet est développé avec **Unity 6000.6.0f1** et utilise principalement des **Behavior Trees** pour gérer la prise de décision des différentes unités.

L'objectif principal est d'améliorer l'intelligence artificielle de l'armée **ROUGE** afin qu'elle puisse battre l'armée **VERTE**, dont le comportement de base ne doit pas être modifié.

---

## 1. Présentation du projet

Le jeu est un prototype de **RTS War Game** dans lequel deux armées s'affrontent sur une carte de 60 m × 60 m.

Deux types d'unités sont présents dans le projet de base :

- **Drones**
  - unités mobiles ;
  - peuvent se déplacer vers une cible ;
  - peuvent s'orienter vers l'ennemi ;
  - tirent des missiles à trajectoire rectiligne.

- **Tourelles d'artillerie**
  - unités statiques ;
  - peuvent s'orienter vers une cible ;
  - tirent des projectiles à trajectoire balistique ;
  - leurs explosions infligent des dégâts de zone.

Composition initiale des armées :

| Armée | Drones | Tourelles |
|---|---:|---:|
| RED | 13 | 6 |
| GREEN | 19 | 9 |

L'armée GREEN est donc volontairement plus puissante au début de la bataille.

---

## 2. Objectifs

Le projet possède deux objectifs principaux.

### Amélioration de l'IA RED

L'équipe doit développer une intelligence artificielle plus performante pour l'armée RED à travers :

- `DroneRedBehavior`
- `TurretRedBehavior`
- `ArmyManagerRed`
- de nouvelles tâches Behavior Designer si nécessaire.

Le comportement de l'armée GREEN sert de référence et ne doit pas être modifié.

### Fonctionnalité différenciante

Le projet final doit également intégrer une nouvelle fonctionnalité, par exemple :

- une nouvelle unité combattante ;
- une nouvelle arme ;
- une unité de soin ;
- une zone permettant de restaurer la santé ;
- ou une autre mécanique originale.

Cette partie est encore en cours de conception / développement.

---

## 3. Technologies utilisées

- Unity `6000.6.0f1`
- C#
- Behavior Designer
- Unity NavMesh
- Git / GitHub

---

## 4. Architecture générale

Les principaux fichiers du projet se trouvent dans :

```text
Assets/
├── Scripts/
│   ├── Army/
│   │   ├── ArmyManager.cs
│   │   ├── ArmyManagerGreen.cs
│   │   ├── ArmyManagerRed.cs
│   │   ├── Drone.cs
│   │   ├── Turret.cs
│   │   ├── Missile.cs
│   │   ├── Rocket.cs
│   │   └── Health.cs
│   │
│   └── MyBehaviorTrees/
│       ├── DroneGreenBehavior.asset
│       ├── DroneRedBehavior.asset
│       ├── TurretGreenBehavior.asset
│       ├── TurretRedBehavior.asset
│       └── ...
│
├── Prefabs/
├── Scenes/
├── Materials/
└── Resources/
```

La scène principale utilisée pour les combats est :

```text
Assets/Scenes/RedVersusGreenBattle.unity
```

---

## 5. IA des tourelles RED

La partie concernant l'artillerie RED a déjà été améliorée par rapport au comportement fourni initialement.

Le Behavior Tree actuel de la tourelle RED suit globalement la structure suivante :

```text
Entry
└── Repeater
    └── Sequence
        ├── Select Red Turret Target
        ├── Turret Seek Target
        ├── Red Turret Shoot
        └── Wait
```

L'objectif est de remplacer la sélection de cible très simple du projet de base par une logique plus tactique et coordonnée.

---

## 6. Sélection tactique des cibles

Une nouvelle tâche a été ajoutée :

```text
SelectRedTurretTarget.cs
```

Elle utilise `ArmyManagerRed` afin de déterminer la cible la plus intéressante pour chaque tourelle RED.

### Priorité aux tourelles ennemies

Les tourelles RED commencent par rechercher les tourelles GREEN à portée.

Les cibles sont sélectionnées en fonction notamment de :

- leur santé restante ;
- les dégâts d'artillerie déjà en cours de route vers cette cible ;
- leur distance.

Cette stratégie permet de concentrer les tirs sur les cibles importantes tout en évitant de gaspiller trop de projectiles.

---

## 7. Gestion des dégâts en cours de route

Les projectiles d'artillerie possèdent un temps de vol important.

Un problème identifié était que plusieurs tourelles RED pouvaient tirer simultanément sur une même cible alors que suffisamment de projectiles étaient déjà en route pour la détruire.

Pour limiter ce phénomène, `ArmyManagerRed` enregistre maintenant les tirs d'artillerie en cours.

Lorsqu'un projectile est lancé :

```text
RedTurretShoot
        ↓
ArmyManagerRed.RegisterPendingArtilleryShot()
```

Le manager estime alors la santé restante future de la cible :

```text
Santé estimée
=
Santé actuelle
-
Dégâts des projectiles RED déjà en vol
```

Si les projectiles déjà lancés devraient être suffisants pour détruire une tourelle ennemie, les autres tourelles RED peuvent immédiatement sélectionner une nouvelle cible.

Cette logique permet de réduire le **overkill** et d'améliorer la répartition des tirs.

---

## 8. Sélection des drones ennemis

Lorsqu'aucune tourelle ennemie pertinente n'est disponible, l'artillerie RED peut attaquer les drones GREEN.

Comme les projectiles d'artillerie ont une trajectoire relativement lente, les drones en mouvement rapide sont difficiles à toucher.

La stratégie actuelle privilégie donc :

1. les drones lents ou presque immobiles ;
2. les groupes de drones proches les uns des autres ;
3. un drone lent isolé ;
4. le drone disponible le plus proche en dernier recours.

Cette stratégie exploite également les dégâts de zone des projectiles d'artillerie.

---

## 9. Réduction du overkill

Une des principales améliorations actuelles est donc la coordination entre les différentes tourelles RED.

Sans coordination :

```text
Turret 1 ─┐
Turret 2 ─┼──> même cible
Turret 3 ─┘
```

Même si un seul ou deux tirs suffisent pour terminer la cible.

Avec la gestion des tirs en cours :

```text
Turret 1 ──> Target A
Turret 2 ──> Target A

Target A considérée comme suffisamment couverte

Turret 3 ──> Target B
Turret 4 ──> Target C
```

Les projectiles réservés sont automatiquement retirés du système après un délai légèrement supérieur à leur temps de vol.

Ainsi, si un projectile rate sa cible ou si la cible survit, elle peut redevenir sélectionnable.

---

## 10. Statistiques d'artillerie

Un système de statistiques a également été ajouté pour comparer les performances des artilleries RED et GREEN.

Le script :

```text
TurretAccuracyStats.cs
```

affiche pendant la bataille :

- nombre total de tirs ;
- nombre de tirs ayant touché au moins une unité ;
- précision ;
- nombre total d'unités touchées ;
- nombre moyen d'unités touchées par tir.

Exemple des indicateurs :

```text
RED ARTILLERY

Shots: 39
Hit Shots: 38
Accuracy: 97.4 %
Units Hit: 56
Average Units / Shot: 1.44
```

Ces statistiques permettent de mesurer directement l'effet des modifications apportées au comportement des tourelles.

---

## 11. Instrumentation des projectiles

Pour mesurer les performances de l'artillerie, quelques ajouts ont été effectués dans les scripts existants.

### `Turret.cs`

Lorsqu'une tourelle tire, le système enregistre un nouveau tir.

### `Rocket.cs`

Lorsqu'un projectile explose, le système détermine :

- si au moins une unité ennemie a été touchée ;
- combien d'unités uniques ont été affectées par l'explosion.

Ces modifications servent uniquement à la collecte de statistiques et ne changent pas les caractéristiques de combat originales des unités.

---

## 12. Résultats actuels

Les premiers tests montrent une nette amélioration de l'efficacité individuelle de l'artillerie RED.

Lors d'un des tests effectués :

| Statistique | RED | GREEN |
|---|---:|---:|
| Shots | 39 | 138 |
| Hit Shots | 38 | 62 |
| Accuracy | 97.4 % | 44.9 % |
| Units Hit | 56 | 86 |
| Average Units / Shot | 1.44 | 0.62 |

Ces résultats montrent notamment que les tirs de l'artillerie RED sont actuellement beaucoup mieux exploités.

Cependant, ces chiffres correspondent à une bataille de test et ne constituent pas encore les statistiques finales du projet.

L'évaluation définitive devra être réalisée sur plusieurs batailles.

---

## 13. État actuel du développement

### Implémenté

- projet Unity fonctionnel ;
- Behavior Tree RED pour les tourelles ;
- sélection tactique des cibles d'artillerie ;
- priorité donnée aux tourelles ennemies ;
- sélection prenant en compte la santé des cibles ;
- détection des projectiles d'artillerie déjà en vol ;
- estimation des dégâts futurs ;
- réduction du overkill ;
- changement automatique de cible lorsqu'une cible est déjà suffisamment couverte ;
- priorité aux drones lents ;
- détection de groupes de drones ;
- exploitation des dégâts de zone ;
- statistiques RED / GREEN pour l'artillerie ;
- mesure de la précision ;
- mesure du nombre d'unités touchées par projectile.

### En cours

- amélioration globale de l'IA de l'armée RED ;
- amélioration du comportement des drones RED ;
- coordination entre les différents types d'unités ;
- conception et développement de la fonctionnalité différenciante ;
- tests sur un plus grand nombre de batailles ;
- équilibrage et optimisation de la stratégie générale.

---

## 14. Fichiers principaux ajoutés ou modifiés

### IA RED

```text
Assets/Scripts/Army/ArmyManagerRed.cs
```

Gestion de la stratégie globale de l'artillerie RED et des tirs en cours.

```text
Assets/Scripts/MyBehaviorTrees/SelectRedTurretTarget.cs
```

Sélection tactique de la cible des tourelles RED.

```text
Assets/Scripts/MyBehaviorTrees/RedTurretShoot.cs
```

Gestion du tir RED et enregistrement des dégâts futurs.

```text
Assets/Scripts/MyBehaviorTrees/TurretRedBehavior.asset
```

Behavior Tree utilisé par les tourelles RED.

### Statistiques

```text
TurretAccuracyStats.cs
```

Affichage et calcul des statistiques d'artillerie.

### Instrumentation

```text
Turret.cs
Rocket.cs
```

Ajouts nécessaires au suivi des tirs et des impacts.

---

## 15. Lancer le projet

### Prérequis

Installer :

```text
Unity 6000.6.0f1
```

Tous les membres de l'équipe doivent utiliser de préférence la même version de Unity.

### Cloner le projet

```bash
git clone https://github.com/TONGTONG0113/Jeux-video.git
```

Ouvrir ensuite le dossier avec Unity Hub.

Unity reconstruira automatiquement les dossiers locaux qui ne sont pas versionnés, notamment :

```text
Library/
Temp/
Logs/
obj/
```

Il est donc normal que ces dossiers ne soient pas présents dans le dépôt Git.

---

## 16. Versioning Git

Les fichiers nécessaires pour reconstruire le projet sont versionnés :

```text
Assets/
Packages/
ProjectSettings/
```

Les fichiers générés automatiquement par Unity sont ignorés à l'aide du fichier :

```text
.gitignore
```

Exemple de workflow après une modification :

```bash
git add .
git commit -m "Improve RED artillery strategy"
git push
```

---

## 17. Remarques

Le projet est toujours en développement.

Les performances actuelles de l'artillerie RED ne représentent donc pas encore les performances finales de l'armée RED complète.

Le comportement de l'armée GREEN reste volontairement celui fourni dans le projet de base afin de conserver une référence identique pour les tests comparatifs.

---

## 18. Auteurs

Projet réalisé dans le cadre du cursus d'ingénieur à l'EPF.

La répartition détaillée du travail entre les membres de l'équipe sera également indiquée dans les Release Notes finales.
