# DroneRedBehavior : contribution de Ruofan

## 1. Constat de départ

L'analyse du projet a montré que le fichier `DroneRedBehavior.asset` était encore **identique à l'arbre vert par défaut** (sélection aléatoire `SelectEnemyDrone` / `SelectEnemyTurret`, puis `MySeek`, `MySlerpRotateAroundY`, `DroneShoot`, `Wait`).
Les tâches `SelectRedDroneTarget` et `RedDroneShoot` existaient, mais aucun arbre ne les utilisait. Les drones rouges n'exploitaient donc ni ArmyManagerRed ni le suivi des dégâts en vol.

Causes identifiées pour les problèmes de la section 12 du README :

| Problème observé | Cause identifiée |
|---|---|
| 12.1 Regroupement des drones | `MySeek` envoie chaque drone **sur la position de sa cible** : quand plusieurs drones visent la même cible ou des cibles proches, ils finissent au même endroit. |
| 12.2 Un missile sur deux rate | `Drone.Shoot()` tire les deux missiles **parallèlement** à l'axe du drone. L'écart latéral entre les deux lanceurs est du même ordre que le rayon du collider de la cible (0,4 m). Avec une petite erreur de visée, l'un des deux missiles passe donc à côté. De plus, la visée se fait sur la position actuelle de la cible et non sur sa position future. |
| 12.3 Drones inactifs | (a) `MySeek` ne calcule la destination qu'une seule fois (`OnStart`). Si la cible est sur une terrasse inaccessible, le chemin est partiel et `HasArrived()` ne devient jamais vrai : la tâche reste en Running indéfiniment. (b) Dans `SelectRedDroneTarget`, `maxRadius = 50 m` : en début de bataille certains drones n'avaient aucun ennemi à portée. (c) La logique de retraite utilisait `retreatPosition`, qui n'était jamais renseignée et valait (0,0,0) : les drones blessés partaient au centre de la carte et y restaient en Failure. |
| 12.4 Vulnérabilité aux dégâts de zone | Les drones restent immobiles pendant la visée, le tir et le `Wait` (2 à 4 s), alors qu'une grenade met 4 s à tomber sur la position visée. |

## 2. Réflexion tactique

Une grenade verte vise la **position actuelle** de sa cible et met **4 s** à arriver. Un drone (10 m/s, 8 m/s²) sort d'un rayon de 7 m en environ 1,3 s.
Le chef d'état-major « voit » les trajectoires (cf. sujet). On peut donc prédire le point d'impact de chaque grenade verte en vol et **esquiver**. C'est la contre-mesure la plus efficace au problème 12.4.

Les tourelles vertes ont un **Min Radius de 5 m** (`Select Enemy`). Un drone placé à moins de 5 m d'une tourelle verte ne peut pas être ciblé par celle-ci. Contre une tourelle, les drones rouges se placent donc à environ 4 m, **dans son angle mort**.

Le rayon de la grenade est de 5 m. Nous imposons donc un **espacement d'environ 6 m** entre drones rouges, pour qu'une seule explosion ne touche pas plusieurs drones.

## 3. Nouvel arbre de comportement

```
Entry
└── Repeater (forever)
    └── Selector "Drone Red Root"
        ├── Sequence "Esquive Grenade"        [Abort : Lower Priority]
        │   ├── Is Grenade Threatened          (Conditional)
        │   └── Red Drone Evade Grenade        (Action)
        └── Sequence "Attaque"
            ├── Select Red Drone Target        (cible via ArmyManagerRed)
            ├── Red Drone Engage               (position de tir espacée)
            ├── Red Drone Aim                  (rotation vers le point anticipé)
            ├── Red Drone Shoot                (tir convergent)
            └── Wait (aléatoire 1 à 2 s, Annexe A)
```

Grâce à l'abort *Lower Priority*, la condition d'esquive est réévaluée à chaque frame pendant toute la branche d'attaque, y compris pendant le `Wait`. Dès qu'une grenade va tomber sur le drone, l'attaque est interrompue et le drone esquive.

## 4. Scripts C#

| Fichier | Rôle |
|---|---|
| `Army/RedDroneTactics.cs` *(nouveau)* | Outils partagés : tir anticipé (profil de vitesse du missile : départ à 10 m/s, accélération 40 m/s², vitesse max 20 m/s), ligne de tir (raycast, alliés traversés, obstacles bloquants), prédiction d'impact des grenades (résolution balistique avec la hauteur du terrain, 3 itérations pour les terrasses), réservation des positions de tir, choix de la position de tir et du point de fuite. |
| `MyBehaviorTrees/SelectRedDroneTarget.cs` *(réécrit)* | Demande la cible à `ArmyManagerRed.RequestTarget` avec une portée de 1000 m. Si le manager ne renvoie rien, prend l'ennemi le plus proche. La retraite cassée est supprimée. |
| `MyBehaviorTrees/RedDroneEngage.cs` *(nouveau)* | Remplace `MySeek`. Cherche une position de tir sur 3 anneaux × 12 angles autour de la cible, notée selon : coût du trajet, espacement avec les drones rouges (positions et destinations réservées), groupes de drones verts, zones d'impact prévues, ligne de tir. Recalcule le chemin si la cible bouge. Détecte les chemins partiels et les blocages. Timeout de 6 s : la tâche ne peut plus rester bloquée. |
| `MyBehaviorTrees/RedDroneAim.cs` *(nouveau)* | Même Slerp que `MySlerpRotateAroundY` (coefficient 4, angle d'arrivée 5°), mais orienté vers le point d'interception anticipé. |
| `MyBehaviorTrees/RedDroneShoot.cs` *(réécrit)* | Tir convergent via `Drone.ShootAt`. Enregistre les dégâts en vol (anti-overkill). Impose au moins 1 s entre deux salves, pour que l'esquive ne permette jamais de tirer plus vite que l'IA d'origine. |
| `MyBehaviorTrees/IsGrenadeThreatened.cs` *(nouveau)* | Conditional : vrai si une grenade ennemie doit tomber à moins de 7 m dans les 2,5 s. |
| `MyBehaviorTrees/RedDroneEvadeGrenade.cs` *(nouveau)* | Fuite hors de toutes les zones d'impact prévues, en restant si possible à portée de la cible et espacé des alliés. |
| `Army/Drone.cs` *(ajout)* | `ShootAt(aimPoint, maxCorrection)` et `MissileCount`. `Shoot()`, utilisé par l'armée verte, n'est pas modifié. |
| `DroneRedBehavior.asset` | Nouvel arbre ci-dessus. |

## 5. Respect des contraintes

- Le comportement de l'armée verte n'est pas modifié : `Shoot()`, `DroneGreenBehavior` et les tâches vertes sont inchangés.
- Les caractéristiques de l'Annexe A sont conservées : vitesse 10 m/s, vitesse angulaire 180 °/s, accélération 8 m/s² (composant inchangé), Slerp Coef 4, Arrival Angle 5°, Wait aléatoire de 1 à 2 s, missiles et grenades inchangés.
- Les missiles restent rectilignes. Seule leur direction initiale converge vers la cible, avec une correction bornée à 8° autour de l'axe du drone. Le drone doit donc toujours s'orienter avant de tirer.
- La distance d'arrêt de 6 m de `MySeek` est remplacée par une position de tir choisie tactiquement (13 m d'un drone, 4 m d'une tourelle). C'est un choix de comportement, pas une caractéristique d'unité.

## 6. Limitations et pistes d'amélioration

- Le tir anticipé suppose que la cible garde sa vitesse actuelle. Le décalage est plafonné à 6 m, car les drones verts changent souvent de direction.
- La prédiction d'impact suppose que la grenade vise le pivot d'un drone (environ 1 m au-dessus du sol). L'erreur horizontale qui en résulte (moins de 1 m) est couverte par la marge de 2 m du rayon de danger.
- `Drone.Heal()` (unité Medic) lit par réflexion le champ `m_Health`, qui est un composant `Health` et non un `float` : le cast lèvera une exception. Il faudrait ajouter une méthode `Heal()` dans `Health.cs`. Ce point est signalé à LIU Yao.
- `ArmyManagerRed.GetBestTargetForRedDrone` ne renvoie aucune tourelle tant qu'elle n'est pas affaiblie. Quand il ne reste que des tourelles vertes en pleine santé, c'est le secours de `SelectRedDroneTarget` qui prend le relais.
- Paramètres à affiner pendant les 20 simulations : `preferredRangeVsDrone`, `allySpacing`, `reactionTime`.
