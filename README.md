# Real-Time Decision Making in Video Games

## 1. Présentation du projet

Ce projet est réalisé dans le cadre du module **Real Time Decision Making in Video Game** à l'EPF pour l'année universitaire 2026–2027.

L'objectif est de développer une intelligence artificielle pour l'armée rouge dans un jeu de stratégie en temps réel de type RTS Wargame, développé avec Unity 3D et C#.

L'armée rouge doit être capable de vaincre l'armée verte contrôlée par l'IA par défaut. Pour atteindre cet objectif, nous améliorons les arbres de comportement des drones et des tourelles d'artillerie, ainsi que la coordination globale des unités.

Le projet comprend également le développement d'une fonctionnalité supplémentaire afin de différencier notre solution.

---

## 2. Objectifs

Les objectifs principaux sont les suivants :

- Développer une IA rouge plus performante que l'IA verte par défaut.
- Améliorer la sélection et la répartition des cibles ennemies.
- Optimiser la coordination entre les drones et les tourelles d'artillerie.
- Réduire les déplacements inutiles et les attaques inefficaces.
- Concevoir des arbres de comportement adaptés aux différents types d'unités.
- Implémenter une fonctionnalité supplémentaire.
- Évaluer les performances de l'IA à partir de simulations et de données statistiques.

---

## 3. État initial et contraintes

Le jeu met en scène deux armées qui s'affrontent sur une carte de 60 × 60 mètres comprenant une plaine, des terrasses surélevées et différents obstacles.

Chaque armée dispose de deux types d'unités :

- **Drones :** unités mobiles capables de se déplacer vers une cible, de s'orienter et de tirer des missiles.
- **Tourelles d'artillerie :** unités statiques capables de s'orienter et de tirer des grenades à trajectoire balistique.

### Effectifs initiaux

| Unité | Armée rouge | Armée verte |
|---|---:|---:|
| Drones | 13 | 19 |
| Tourelles d'artillerie | 6 | 9 |

L'armée rouge est donc en infériorité numérique. Notre stratégie consiste à compenser ce désavantage grâce à une meilleure prise de décision et à une utilisation plus efficace des unités.

### Contraintes techniques

- Le comportement de l'armée verte ne doit pas être modifié.
- Les caractéristiques des unités définies dans l'annexe A du sujet ne doivent pas être modifiées.
- Le développement doit respecter l'architecture existante du projet Unity.
- Les modifications doivent être testées afin de vérifier leur impact sur les performances de l'armée rouge.

---

## 4. Stratégie générale

Notre stratégie repose sur trois principes :

1. **Concentration des forces :** concentrer les attaques sur des cibles prioritaires plutôt que de disperser les unités.
2. **Coordination des unités :** améliorer la coopération entre les drones, les tourelles et le gestionnaire de l'armée.
3. **Adaptation au champ de bataille :** utiliser les informations disponibles sur les positions, les trajectoires, les points de vie, les obstacles et le terrain pour ajuster les décisions.

L'objectif est de maximiser l'efficacité de chaque unité sans modifier ses caractéristiques de base.

---

## 5. Stratégies tactiques

### 5.1. Priorisation des cibles

La sélection des cibles ne repose plus uniquement sur un choix simple ou aléatoire.

Pour l'artillerie rouge, une sélection tactique a déjà été implémentée.

Les critères actuellement utilisés comprennent notamment :

- Le type d'unité ennemie.
- La distance de la cible.
- Ses points de vie restants.
- Les dégâts des projectiles d'artillerie déjà en vol vers cette cible.
- La vitesse des drones ennemis.
- La présence de plusieurs drones dans une même zone.

Les tourelles ennemies sont actuellement considérées comme des cibles prioritaires.

Lorsqu'aucune tourelle pertinente n'est disponible, l'artillerie peut sélectionner des drones en privilégiant les unités lentes ou regroupées.

---

### 5.2. Concentration des attaques des drones

L'amélioration du comportement des drones rouges est en cours.

L'objectif est d'utiliser une sélection de cibles plus cohérente afin de limiter la dispersion des tirs et les déplacements inutiles.

Le comportement prévu comprend notamment :

1. Vérifier que la cible est valide et toujours active.
2. Évaluer les cibles disponibles.
3. Sélectionner une cible prioritaire.
4. Se déplacer vers la cible et s'orienter correctement.
5. Tirer les missiles lorsque les conditions d'attaque sont réunies.
6. Vérifier l'état de la cible après l'attaque.
7. Choisir une nouvelle cible si nécessaire.

Cette approche vise à réduire les attaques redondantes et les poursuites inutiles.

---

### 5.3. Optimisation des tourelles d'artillerie

L'intelligence artificielle des tourelles d'artillerie rouges a été améliorée.

Le comportement actuel utilise une nouvelle logique de sélection des cibles ainsi qu'une coordination des tirs entre les différentes tourelles.

Les améliorations implémentées comprennent :

- La priorité donnée aux tourelles ennemies.
- La prise en compte des points de vie de la cible.
- La prise en compte des projectiles d'artillerie déjà en vol.
- L'estimation des dégâts futurs sur une cible.
- La réduction du **overkill**.
- Le changement de cible lorsqu'un nombre suffisant de projectiles est déjà en route pour détruire une cible.
- La priorité donnée aux drones lents ou presque immobiles.
- La détection des groupes de drones.
- L'exploitation des dégâts de zone des grenades d'artillerie.
- La réévaluation régulière de la cible.

Cette stratégie permet de mieux répartir les tirs entre plusieurs cibles et de limiter le gaspillage de projectiles.

---

### 5.4. Gestion des projectiles déjà en vol

Une logique spécifique a été ajoutée dans `ArmyManagerRed` afin de suivre les tirs d'artillerie déjà effectués.

Lorsqu'une tourelle rouge tire sur une tourelle ennemie, le tir est enregistré comme un projectile en cours.

Le système estime ensuite la santé future de la cible :

```text
Santé estimée
=
Santé actuelle
-
Dégâts des projectiles déjà en vol
```

Si les dégâts prévus sont suffisants pour détruire la cible, les autres tourelles peuvent immédiatement sélectionner un autre ennemi.

Les tirs enregistrés sont supprimés après un délai légèrement supérieur au temps de vol du projectile.

Ainsi, si un projectile rate sa cible ou si celle-ci survit, elle peut redevenir sélectionnable.

---

### 5.5. Ciblage des drones

Les drones en mouvement rapide sont difficiles à toucher avec l'artillerie à cause du temps de vol des projectiles.

Pour cette raison, l'artillerie rouge privilégie actuellement :

1. Les groupes de drones lents ou presque immobiles.
2. Les drones lents isolés.
3. Les drones disponibles les plus proches lorsqu'aucune meilleure cible n'existe.

La recherche de groupes permet d'exploiter les dégâts de zone des explosions.

---

### 5.6. Coordination globale de l'armée

Le gestionnaire `ArmyManagerRed` constitue le centre de décision de l'armée rouge.

Il peut exploiter les informations disponibles concernant :

- Les positions et les trajectoires des unités alliées et ennemies.
- Les points de vie des unités.
- Les missiles et les grenades présents sur le champ de bataille.
- Les obstacles et les caractéristiques du terrain.

Une première partie de cette coordination est déjà utilisée pour l'artillerie afin de :

- Répartir les cibles.
- Suivre les tirs déjà en cours.
- Limiter les tirs inutiles sur une cible déjà condamnée.
- Adapter le choix de cible en fonction de l'évolution du combat.

La coordination globale entre les drones et les tourelles pourra encore être améliorée.

Le gestionnaire ne connaît pas les intentions de l'ennemi. Les décisions reposent donc uniquement sur les informations observables disponibles.

---

## 6. Architecture de l'intelligence artificielle

Le développement repose principalement sur trois composants.

### 6.1. `ArmyManagerRed`

`ArmyManagerRed` centralise une partie des décisions tactiques de l'armée rouge.

Pour l'artillerie, les fonctionnalités suivantes sont déjà implémentées :

- Évaluation des cibles disponibles.
- Sélection tactique des cibles.
- Prise en compte des points de vie.
- Suivi des projectiles déjà en vol.
- Estimation des dégâts futurs.
- Réduction du overkill.
- Répartition plus efficace des tirs entre plusieurs cibles.
- Détection de drones lents.
- Détection de groupes de drones.

D'autres fonctions de coordination pourront être ajoutées pour les drones et pour la stratégie globale de l'armée.

---

### 6.2. `DroneRedBehavior`

L'amélioration du Behavior Tree des drones rouges est en cours.

L'objectif est de structurer leur comportement autour des étapes suivantes :

- Vérification de l'état du drone.
- Sélection d'une cible.
- Vérification de la validité de la cible.
- Déplacement vers la cible.
- Orientation et attaque.
- Réévaluation de la situation.
- Sélection d'une nouvelle cible si nécessaire.

---

### 6.3. `TurretRedBehavior`

Le Behavior Tree des tourelles rouges a été modifié.

Sa structure principale est actuellement :

```text
Entry
└── Repeater
    └── Sequence
        ├── Select Red Turret Target
        ├── Turret Seek Target
        ├── Red Turret Shoot
        └── Wait
```

Deux nouvelles tâches sont utilisées :

- `SelectRedTurretTarget` : sélection tactique de la cible.
- `RedTurretShoot` : tir de la tourelle et enregistrement du projectile en cours.

Les arbres de comportement sont développés avec Behavior Designer.

---

## 7. Fonctionnalité supplémentaire

Une fonctionnalité supplémentaire devra être développée afin d'apporter une valeur ajoutée au projet.

### Proposition : unité mobile de réparation

Une unité de soutien pourrait être ajoutée à l'armée rouge afin de restaurer les points de vie des unités alliées endommagées.

Le comportement envisagé comprendrait :

- La détection des unités alliées nécessitant une réparation.
- La sélection d'une unité prioritaire.
- Le déplacement vers cette unité.
- La réparation lorsque les conditions nécessaires sont réunies.
- Le choix d'une nouvelle cible lorsque la réparation est terminée ou impossible.

Cette fonctionnalité est encore une proposition à valider.

Une alternative plus simple serait la création d'une zone de réparation dans laquelle les unités alliées pourraient récupérer des points de vie.

---

## 8. État actuel du développement

### Implémenté

- Analyse du projet Unity existant.
- Modification de `TurretRedBehavior`.
- Nouvelle sélection tactique des cibles pour l'artillerie rouge.
- Priorité aux tourelles ennemies.
- Prise en compte des points de vie des cibles.
- Suivi des projectiles d'artillerie déjà en vol.
- Estimation des dégâts futurs.
- Réduction du overkill.
- Changement automatique de cible lorsqu'une cible est déjà suffisamment couverte.
- Priorité aux drones lents.
- Détection des groupes de drones.
- Exploitation des dégâts de zone.

### En cours / à réaliser

- Optimisation complète de `DroneRedBehavior`.
- Amélioration de la coordination globale de l'armée.
- Développement de la fonctionnalité supplémentaire.
- Intégration et débogage.
- Tests sur plusieurs batailles.
- Simulations finales sur 20 batailles valides.
- Analyse statistique finale.
- Préparation de la vidéo et de la livraison.

---

## 9. Plan de développement

| Étape | Travail | État |
|---|---|---|
| 1 | Analyse du projet Unity existant | Terminé |
| 2 | Premiers tests | Réalisé partiellement |
| 3 | Développement de `ArmyManagerRed` | En cours — logique d'artillerie implémentée |
| 4 | Optimisation de `DroneRedBehavior` | En cours |
| 5 | Optimisation de `TurretRedBehavior` | Implémentée |
| 6 | Développement de la fonctionnalité supplémentaire | À réaliser |
| 7 | Intégration et débogage | En cours |
| 8 | Simulations et analyse | À poursuivre |
| 9 | Préparation de la livraison | À réaliser |

---

## 10. Stratégie de test et critères d'évaluation

Les performances seront évaluées à partir de simulations contre l'IA verte par défaut.

Conformément au sujet, **20 batailles valides** devront être simulées pour l'évaluation finale.

Les indicateurs suivis seront :

- **Taux de victoire :** proportion de batailles remportées par l'armée rouge.
- **Nombre de drones survivants :** nombre de drones encore en vie à la fin du combat.
- **Nombre de tourelles survivantes :** nombre de tourelles encore en vie à la fin du combat.
- **Points de vie cumulés restants :** total des points de vie des unités survivantes.
- **Durée de la bataille :** temps nécessaire pour terminer le combat.

Les situations de blocage (*deadlock*) identifiées comme non comptabilisables seront relancées conformément aux consignes.

---

## 11. Répartition du travail

Le projet est réalisé par une équipe de **trois étudiants**.

| Membre | Responsabilités principales |
|---|---|
| Membre 1 | `ArmyManagerRed`, stratégie globale et coordination de l'armée |
| **Ruofan** | `DroneRedBehavior` et logique des drones |
| **CHENG Zihao** | `TurretRedBehavior` et logique des tourelles d'artillerie |

L'intégration, les tests, la validation et le débogage sont réalisés collectivement.

La répartition pourra être ajustée en fonction de l'avancement du projet et des contributions réelles de chaque membre.

---

## 12. Fichiers principaux liés à l'artillerie RED

### `ArmyManagerRed.cs`

Contient notamment :

- La logique de sélection tactique des cibles.
- Le suivi des tirs d'artillerie en cours.
- L'estimation des dégâts futurs.
- La réduction du overkill.
- La sélection des drones lents et des groupes de drones.

### `SelectRedTurretTarget.cs`

Tâche Behavior Designer utilisée pour demander à `ArmyManagerRed` la meilleure cible disponible.

### `RedTurretShoot.cs`

Tâche spécifique aux tourelles rouges permettant :

- De tirer sur la cible sélectionnée.
- D'enregistrer le projectile comme un tir en cours.

### `TurretRedBehavior.asset`

Behavior Tree utilisé par les tourelles rouges.

---

## 13. Résultats attendus

Le projet vise à obtenir :

- Une armée rouge capable de prendre des décisions tactiques plus cohérentes.
- Une meilleure coordination entre les unités.
- Une réduction des attaques inefficaces.
- Une utilisation plus efficace des projectiles d'artillerie.
- Des arbres de comportement structurés et faciles à maintenir.
- Une fonctionnalité supplémentaire opérationnelle.
- Des résultats statistiques permettant d'évaluer objectivement les améliorations.

Les résultats définitifs devront être confirmés par les simulations finales.

---

## 14. Technologies utilisées

- **Unity 3D** : moteur de jeu.
- **C#** : langage de programmation.
- **Behavior Designer** : conception des arbres de comportement.
- **Git / GitHub** : gestion du code source et collaboration.
- **CSV / tableur** : collecte et analyse des résultats de simulation.

---

## 15. Conclusion

Notre approche consiste à améliorer progressivement la prise de décision de l'armée rouge tout en conservant les caractéristiques originales des unités.

La partie concernant les tourelles d'artillerie rouges dispose déjà d'une sélection tactique des cibles, d'un mécanisme de coordination des tirs et d'une réduction du overkill.

Les prochaines étapes concernent principalement l'intégration avec le comportement des drones, la fonctionnalité supplémentaire et les simulations finales.

Le développement reste guidé par une démarche expérimentale : modifier les comportements, mesurer les résultats et conserver les améliorations dont l'efficacité peut être démontrée.
