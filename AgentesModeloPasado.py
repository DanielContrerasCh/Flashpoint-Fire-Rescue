# -*- coding: utf-8 -*-
"""main.ipynb

Automatically generated by Colab.

Original file is located at
    https://colab.research.google.com/drive/1QaLXz8FrGVO5Gra1XKILUjEnvGUJd8FQ

# Flash Point Fire Rescue: Actividad Integradora

Hecho por:
- Daniel Contreras Chávez A01710608
- Daniel Queijeiro Albo A01710441
"""

# %pip install mesa seaborn --quiet

# Commented out IPython magic to ensure Python compatibility.
from mesa import Agent, Model
from mesa.space import MultiGrid
from mesa.time import SimultaneousActivation
from mesa.datacollection import DataCollector
from mesa.batchrunner import batch_run
import random
# %matplotlib inline
import matplotlib
import matplotlib.pyplot as plt
import matplotlib.animation as animation
from matplotlib.colors import ListedColormap
import seaborn as sns
plt.rcParams["animation.html"] = "jshtml"
matplotlib.rcParams['animation.embed_limit'] = 2**128
import numpy as np
import pandas as pd
import copy

def get_distance(pos1, pos2):
    """
    Calcula la distancia Euclidiana entre dos posiciones.

    Args:
        pos1 (tuple): Coordenadas (fila, columna) de la primera posición.
        pos2 (tuple): Coordenadas (fila, columna) de la segunda posición.

    Returns:
        float: Distancia Euclidiana entre pos1 y pos2.
    """
    x = pos1[0] - pos2[0]
    y = pos1[1] - pos2[1]
    d = np.sqrt(x**2 + y**2)
    return d

def find_door(current_pos, next_pos, doors):
    """
    Encuentra la puerta entre dos posiciones si existe.

    Args:
        current_pos (tuple): Posición actual (row, col).
        next_pos (tuple): Posición objetivo (row, col).
        doors (list): Lista de puertas.

    Returns:
        dict or None: Diccionario de la puerta si existe, de lo contrario None.
    """
    for door in doors:
        if ((door['row1'], door['col1']) == current_pos and (door['row2'], door['col2']) == next_pos) or \
           ((door['row2'], door['col2']) == current_pos and (door['row1'], door['col1']) == next_pos):
            return door
    return None

def can_move(current_pos, next_pos, walls_grid, doors):
    """
    Verifica si se puede mover de current_pos a next_pos considerando paredes y puertas.

    Args:
        current_pos (tuple): Posición actual (row, col).
        next_pos (tuple): Posición objetivo (row, col).
        walls_grid (list): Grid de paredes.
        doors (list): Lista de puertas.

    Returns:
        bool: True si se puede mover, False de lo contrario.
    """
    current_row, current_col = current_pos
    next_row, next_col = next_pos

    delta_row = next_row - current_row
    delta_col = next_col - current_col

    # Validar que el movimiento es adyacente (no diagonal)
    if (delta_row, delta_col) not in [(-1, 0), (1, 0), (0, -1), (0, 1)]:
        return False

    walls = walls_grid[current_row][current_col].zfill(4)  # Asegura que tiene 4 caracteres

    # Determinar la dirección del movimiento
    if delta_row == -1 and delta_col == 0:  # Arriba
        wall_index = 0
    elif delta_row == 0 and delta_col == 1:  # Derecha
        wall_index = 1
    elif delta_row == 1 and delta_col == 0:  # Abajo
        wall_index = 2
    elif delta_row == 0 and delta_col == -1:  # Izquierda
        wall_index = 3
    else:
        return False  # Movimiento inválido

    if walls[wall_index] == "1":
        # Buscar si hay una puerta entre current_pos y next_pos
        door = find_door(current_pos, next_pos, doors)
        if door:
            return door['is_open']
        else:
            return False  # Pared sin puerta
    else:
        return True  # No hay pared en esa dirección

class FireFighterAgent(Agent):
    def __init__(self, id, model, ap=4):
        super().__init__(id, model)
        self.ap = ap
        self.is_carrying = False
        self.target_entrance = None  # Entrada objetivo
        self.path_to_exit = []  # Ruta hacia la entrada objetivo
        self.assigned_POI = None  # POI asignado al agente

    def step(self):
            if self.ap <= 0:
                return

            if self.is_carrying:
                if self.target_entrance is None:
                    self.get_nearest_entrance()
                self.rescue_victim()
            else:
                if self.assigned_POI is None:
                    # Intentar asignar un POI
                    self.model.assign_POI(self)

                if self.assigned_POI:
                    self.move_towards_poi()
                else:
                    self.move_randomly()

    def move_towards_poi(self):
        """
        Mueve al agente hacia el POI asignado.
        """
        if self.pos == self.assigned_POI:
            # Llegó al POI
            self.is_carrying = True
            # Actualizar el estado del POI en markers
            for marker in self.model.markers:
                if marker['row'] == self.assigned_POI[0] and marker['col'] == self.assigned_POI[1]:
                    marker['revealed'] = True
                    break
            if self.assigned_POI in self.model.assigned_POIs:
                self.model.assigned_POIs.remove(self.assigned_POI)
            self.assigned_POI = None

        else:
            # Mover hacia el POI asignado
            possible_positions = self.model.grid.get_neighborhood(self.pos, moore=False, include_center=False)
            possible_positions = list(possible_positions)
            np.random.shuffle(possible_positions)  # Mezclar para aleatoriedad

            current_distance = get_distance(self.pos, self.assigned_POI)
            moved = False

            for position in possible_positions:
                # Verificar si la posición es accesible (sin pared o puerta abierta)
                can_move, door = self.can_move(self.pos, position, self.model.walls_grid, self.model.doors)
                if can_move:
                    new_distance = get_distance(position, self.assigned_POI)
                    if new_distance < current_distance:
                        # Mover al agente
                        self.model.grid.move_agent(self, position)
                        self.ap -= 1
                        moved = True
                        break
                elif door is not None and door['is_open']:
                    new_distance = get_distance(position, self.assigned_POI)
                    if new_distance < current_distance:
                        # Mover al agente a través de la puerta abierta
                        self.model.grid.move_agent(self, position)
                        self.ap -= 1
                        moved = True
                        break

            if not moved:
                # Si no puede moverse hacia una posición que reduce la distancia, realizar un movimiento aleatorio
                self.move_randomly()

    def get_nearest_entrance(self):
        """
        Identifica la entrada más cercana al agente basado en la distancia Euclidiana.
        """
        min_distance = float('inf')
        closest_entrance = None

        for entrance in self.model.entrances:
            entrance_pos = (entrance['row'], entrance['col'])
            distance = get_distance(self.pos, entrance_pos)
            if distance < min_distance:
                min_distance = distance
                closest_entrance = entrance_pos

        if closest_entrance:
            self.target_entrance = closest_entrance

    def rescue_victim(self):
        """
        Mueve al agente hacia la entrada más cercana, asegurándose de que cada movimiento reduce la distancia al objetivo.
        Considera paredes y puertas al determinar movimientos permitidos.
        """
        if not self.target_entrance:
            return
        if self.pos == self.target_entrance:
            self.is_carrying = False
            self.target_entrance = None
            self.model.rescued_victims += 1
        else:
            possible_positions = self.model.grid.get_neighborhood(self.pos, moore=False, include_center=False)
            possible_positions = list(possible_positions)
            np.random.shuffle(possible_positions)  # Mezclar para aleatoriedad

            current_distance = get_distance(self.pos, self.target_entrance)
            moved = False

            for position in possible_positions:
                # Verificar si la posición es accesible (sin pared o puerta abierta)
                can_move, door = self.can_move(self.pos, position, self.model.walls_grid, self.model.doors)
                if can_move:
                    new_distance = get_distance(position, self.target_entrance)
                    if new_distance < current_distance:
                        # Mover al agente
                        self.model.grid.move_agent(self, position)
                        self.ap -= 1
                        moved = True
                        break
                elif door is not None and door['is_open']:
                    new_distance = get_distance(position, self.target_entrance)
                    if new_distance < current_distance:
                        # Mover al agente a través de la puerta abierta
                        self.model.grid.move_agent(self, position)
                        self.ap -= 1
                        moved = True
                        break

            if not moved:
                # Si no puede moverse hacia una posición que reduce la distancia, realizar un movimiento aleatorio
                self.move_randomly()

    def move_randomly(self):
        """
        Movimiento aleatorio del agente cuando no está cargando una víctima.
        """
        possible_positions = self.model.grid.get_neighborhood(self.pos, moore=False, include_center=False)
        possible_positions = list(possible_positions)
        np.random.shuffle(possible_positions)  # Mezclar para aleatoriedad

        for position in possible_positions:
            can_move, door = self.can_move(self.pos, position, self.model.walls_grid, self.model.doors)
            if can_move:
                self.model.grid.move_agent(self, position)
                self.ap -= 1
                break
            elif door is not None:
                if self.ap >= 1:
                    door['is_open'] = True  # Abrir la puerta
                    self.model.grid.move_agent(self, position)
                    self.ap -= 1
                    break
                else:
                    continue  # No tiene suficiente AP para abrir la puerta

    def can_move(self, current_pos, next_pos, walls_grid, doors):
        """
        Verifica si el agente puede moverse a la posición deseada considerando paredes y puertas.

        Args:
            current_pos (tuple): Posición actual del agente.
            next_pos (tuple): Posición objetivo.
            walls_grid (list): Grid de paredes.
            doors (list): Lista de puertas.

        Returns:
            tuple: (bool, door) donde bool indica si puede moverse y door es la puerta si existe.
        """
        current_row, current_col = current_pos
        next_row, next_col = next_pos

        delta_row = next_row - current_row
        delta_col = next_col - current_col

        walls = walls_grid[current_row][current_col].zfill(4)  # Asegura que tiene 4 caracteres

        # Determinar la dirección del movimiento
        if delta_row == -1 and delta_col == 0:  # Arriba
            wall_index = 0
        elif delta_row == 0 and delta_col == 1:  # Derecha
            wall_index = 1
        elif delta_row == 1 and delta_col == 0:  # Abajo
            wall_index = 2
        elif delta_row == 0 and delta_col == -1:  # Izquierda
            wall_index = 3
        else:
            return False, None  # Movimiento inválido

        if walls[wall_index] == "1":
            # Buscar si hay una puerta entre current_pos y next_pos
            door = self.find_door(current_pos, next_pos, doors)
            if door:
                if door['is_open']:
                    return True, door
                else:
                    return False, door
            else:
                return False, None  # Pared sin puerta
        else:
            return True, None  # No hay pared en esa dirección

    def find_door(self, current_pos, next_pos, doors):
        """
        Encuentra la puerta entre dos posiciones si existe.

        Args:
            current_pos (tuple): Posición actual.
            next_pos (tuple): Posición objetivo.
            doors (list): Lista de puertas.

        Returns:
            dict or None: Diccionario de la puerta si existe, de lo contrario None.
        """
        for door in doors:
            if ((door['row1'], door['col1']) == current_pos and (door['row2'], door['col2']) == next_pos) or \
               ((door['row2'], door['col2']) == current_pos and (door['row1'], door['col1']) == next_pos):
                return door
        return None

def get_grid(model):
    grid = np.zeros((model.grid.width, model.grid.height))  # Inicializar el grid con ceros

    for agent in model.schedule.agents:
        x, y = agent.pos
        if agent.is_carrying:
            grid[x][y] = 1  # Verde para agentes que están cargando una víctima
        else:
            grid[x][y] = 2  # Azul para agentes que no están cargando

    return grid

def get_doors_state(model):
    return copy.deepcopy(model.doors)

def get_poi(model):
    """
    Devuelve una tupla con el estado de cada POI.
    Cada POI se representa como (row, col, type, revealed).
    """
    return tuple((marker['row'], marker['col'], marker['type'], marker['revealed']) for marker in model.markers)

def get_fires_state(model):
    """
    Devuelve una lista con la posición de cada fuego activo.

    Returns:
        list of dict: Lista con información de cada fuego.
    """
    return [
        {"row": pos[0], "col": pos[1]}
        for pos in model.fire_positions
    ]

def get_smokes_state(model):
    """
    Devuelve una lista con la posición de cada humo activo.

    Returns:
        list of dict: Lista con información de cada humo.
    """
    return [
        {"row": pos[0], "col": pos[1]}
        for pos in model.smoke_positions
    ]

class BoardModel(Model):
    def __init__(self, width, height, walls, doors, entrances, markers, fire_markers):
        super().__init__()
        self.width = width
        self.height = height
        self.walls_grid = walls
        self.doors = doors
        self.entrances = entrances
        self.markers = markers
        self.fire_positions = []
        self.smoke_positions = []
        self.aux = 0
        self.rescued_victims = 0
        self.assigned_POIs = []  # Lista para rastrear POIs asignados
        self.grid = MultiGrid(width, height, True)
        self.schedule = SimultaneousActivation(self)
        self.steps = 0
        self.current_agent_index = 0
        self.datacollector = DataCollector(
            model_reporters={
                "Grid": get_grid,
                "Doors": get_doors_state,
                "POI": get_poi,
                "Fires": get_fires_state,
                "Smokes": get_smokes_state,  # Añadido para humos
            },
        )

        # Crear todos los agentes y agregarlos a la lista de agentes por añadir
        self.agents_to_add = []
        for i in range(6):
            agent = FireFighterAgent(i, self)
            self.agents_to_add.append(agent)

        # Inicializar posiciones de fuego
        for fire in fire_markers:
            position = (fire['row'], fire['col'])
            self.fire_positions.append(position)
            print(f"Fuego inicializado en {position}.")

    def assign_POI(self, agent):
        """
        Asigna el POI más cercano disponible al agente.

        Args:
            agent (FireFighterAgent): El agente que solicita la asignación.

        Returns:
            tuple or None: Coordenadas (row, col) del POI asignado o None si no hay disponibles.
        """
        available_POIs = [
            (marker['row'], marker['col'])
            for marker in self.markers
            if not marker['revealed'] and (marker['row'], marker['col']) not in self.assigned_POIs
        ]

        if not available_POIs:
            return None  # No hay POIs disponibles

        # Encontrar el POI más cercano
        closest_POI = min(
            available_POIs,
            key=lambda poi: get_distance(agent.pos, poi)
        )

        # Asignar el POI al agente
        self.assigned_POIs.append(closest_POI)
        agent.assigned_POI = closest_POI

        return closest_POI

    def add_smoke(self):
        """
        Añade un humo en una posición aleatoria del grid y maneja las interacciones con otros humos y fuegos.
        """
        # Elegir una posición aleatoria
        random_row = random.randint(0, self.width - 1)
        random_col = random.randint(0, self.height - 1)
        random_pos = (random_row, random_col)

        # 1. Verificar si el humo se añade en una posición con fuego
        if random_pos in self.fire_positions:
            print(f"¡Explosión en {random_pos}!")
            self.handle_explosion(random_pos)
            return

        # 2. Verificar si ya hay un humo en esa posición
        if random_pos in self.smoke_positions:
            # Eliminar el humo existente
            self.smoke_positions.remove(random_pos)
            # Añadir fuego en esta posición
            self.fire_positions.append(random_pos)
            print(f"Dos humos en {random_pos} desaparecen y se añade un fuego.")
            return

        # 3. Verificar si la posición está adyacente a algún fuego con conexión válida
        adjacent_positions = self.get_adjacent_positions(random_pos)
        for adj in adjacent_positions:
            if adj in self.fire_positions:
                # Verificar si hay una pared o una puerta cerrada entre random_pos y adj
                can_comm = can_move(random_pos, adj, self.walls_grid, self.doors)
                if can_comm:
                    # Añadir fuego en esta posición
                    self.fire_positions.append(random_pos)
                    print(f"Humo en {random_pos} estaba adyacente a fuego con conexión válida. Se añade fuego.")
                    return

        # 4. Si ninguna de las condiciones anteriores se cumple, añadir el humo
        self.smoke_positions.append(random_pos)
        print(f"Humo añadido en {random_pos}.")

    def get_adjacent_positions(self, pos):
        """
        Devuelve las posiciones adyacentes (arriba, abajo, izquierda, derecha) de una posición dada.

        Args:
            pos (tuple): Coordenadas (row, col) de la posición actual.

        Returns:
            list of tuple: Lista con las coordenadas de las posiciones adyacentes.
        """
        row, col = pos
        adjacent = []

        if row > 0:
            adjacent.append((row - 1, col))  # Arriba
        if row < self.height - 1:
            adjacent.append((row + 1, col))  # Abajo
        if col > 0:
            adjacent.append((row, col - 1))  # Izquierda
        if col < self.width - 1:
            adjacent.append((row, col + 1))  # Derecha

        return adjacent

    def handle_explosion(self, pos):
        """
        Maneja la explosión que ocurre en la posición `pos`.
        Propaga el fuego en las cuatro direcciones hasta encontrar una pared o una puerta cerrada.

        Args:
            pos (tuple): Coordenadas (row, col) donde ocurre la explosión.
        """
        directions = [(-1, 0), (1, 0), (0, -1), (0, 1)]  # Arriba, Abajo, Izquierda, Derecha
        for d_row, d_col in directions:
            current_pos = pos
            while True:
                next_pos = (current_pos[0] + d_row, current_pos[1] + d_col)

                # Verificar que next_pos está dentro de los límites del grid
                if not (0 <= next_pos[0] < self.height and 0 <= next_pos[1] < self.width):
                    break  # Fuera del grid

                # Verificar si se puede mover desde current_pos a next_pos
                if can_move(current_pos, next_pos, self.walls_grid, self.doors):
                    # Añadir fuego en next_pos
                    if next_pos not in self.fire_positions:
                        if next_pos in self.smoke_positions:
                            self.smoke_positions.remove(next_pos)
                            self.fire_positions.append(next_pos)
                            print(f"Smoke at {next_pos} replaced with fire due to explosion.")
                        else:
                            self.fire_positions.append(next_pos)
                            print(f"Fire added at {next_pos} due to explosion.")
                    # Continuar propagando en la dirección
                    current_pos = next_pos
                else:
                    # Si no se puede mover, detener la propagación en esta dirección
                    print(f"Explosion propagation stopped at {next_pos} due to wall or closed door.")
                    break
        self.process_fire_adjacent_smoke()

    def process_fire_adjacent_smoke(self):
        """
        Procesa y convierte los humos adyacentes al fuego en fuego,
        respetando las paredes y puertas cerradas, creando una reacción en cadena.
        """
        conversion_occurred = True  # Flag para controlar la iteración

        while conversion_occurred:
            conversion_occurred = False
            # Copia de la lista actual de fuegos para evitar modificar la lista mientras se itera
            current_fires = copy.deepcopy(self.fire_positions)

            for fire_pos in current_fires:
                adjacent_positions = self.get_adjacent_positions(fire_pos)

                for adj in adjacent_positions:
                    if adj in self.smoke_positions:
                        # Verificar si hay una pared o una puerta cerrada entre fire_pos y adj
                        if can_move(fire_pos, adj, self.walls_grid, self.doors):
                            # Convertir humo en fuego
                            self.smoke_positions.remove(adj)
                            self.fire_positions.append(adj)
                            conversion_occurred = True
                            print(f"Smoke at {adj} converted to fire due to adjacency with fire at {fire_pos}.")
            # Si en una iteración no se convirtió ningún humo, se detiene el bucle

    def step(self):
        # Verificar si aún hay agentes por añadir y si el agente actual ha terminado su turno
        if self.current_agent_index < len(self.agents_to_add):
            # Verificar si el agente actual ya está en el scheduler
            if self.current_agent_index >= len(self.schedule.agents):
                # Obtener el siguiente agente a añadir
                agent_to_add = self.agents_to_add[self.current_agent_index]

                # Seleccionar una entrada aleatoria
                entrance = self.entrances[self.aux]

                # Convertir las coordenadas de entrada a las del grid (0-based indexing)
                entrance_pos = (entrance['row'], entrance['col'])

                # Validar que la posición de la entrada esté dentro de los límites del grid
                if 0 <= entrance_pos[0] < self.width and 0 <= entrance_pos[1] < self.height:
                    # Colocar el agente en la entrada
                    self.grid.place_agent(agent_to_add, entrance_pos)

                    # Añadir el agente al scheduler
                    self.schedule.add(agent_to_add)

        # Verificar si hay un agente activo
        if self.current_agent_index < len(self.schedule.agents):
            # Obtener el agente actual
            current_agent = self.schedule.agents[self.current_agent_index]

            # Verificar si el agente tiene AP disponible
            if current_agent.ap > 0:
                # El agente realiza una acción
                current_agent.step()
            else:
                # El agente ha agotado sus AP y termina su turno
                print(f"Agente {current_agent.unique_id} ha terminado su turno.")
                self.add_smoke()
                current_agent.ap = 4  # Restablecer los AP del agente
                # Pasar al siguiente agente
                if self.current_agent_index < 5:
                    self.current_agent_index += 1
                else:
                    self.current_agent_index = 0
                if self.aux < 3:
                    self.aux += 1
                else:
                    self.aux = 0

        # Recolectar datos y avanzar el contador de pasos
        self.datacollector.collect(self)
        self.steps += 1

def parse_file(filename):
    walls_grid = []
    markers = []
    fire_markers = []
    doors = []
    entrances = []
    with open(filename, 'r') as file:
        # Leer las primeras 6 líneas para el grid
        for _ in range(6):
            line = file.readline().strip()
            walls = line.split()
            walls_grid.append(walls)
        # Leer los marcadores de POI
        for _ in range(3):
            line = file.readline().strip()
            if line:
                row, col, marker_type = line.split()
                markers.append({'row': int(row) - 1, 'col': int(col) - 1, 'type': marker_type, 'revealed': False})

        # Leer los marcadores de fuego
        for _ in range(10):
            line = file.readline().strip()
            if line:
                row, col = line.split()
                fire_markers.append({'row': int(row) - 1, 'col': int(col) - 1})
        # Leer las puertas
        for _ in range(8):
            line = file.readline().strip()
            if line:
                row1, col1, row2, col2 = line.split()
                doors.append({
                    'row1': int(row1) - 1,
                    'col1': int(col1) - 1,
                    'row2': int(row2) - 1,
                    'col2': int(col2) - 1,
                    'is_open': False  # Por defecto, las puertas están cerradas
                })
        # Leer las entradas
        for _ in range(4):
            line = file.readline().strip()
            if line:
                row, col = line.split()
                entrances.append({'row': int(row) - 1, 'col': int(col) - 1})
    return walls_grid, markers, fire_markers, doors, entrances

def draw_smoke(ax, smokes, num_rows):
    """
    Dibuja los humos en el grid.

    Args:
        ax (matplotlib.axes.Axes): Eje de Matplotlib donde dibujar.
        smokes (list of dict): Lista con información de los humos.
        num_rows (int): Número de filas del grid para ajustar coordenadas.
    """
    for smoke in smokes:
        row, col = smoke['row'], smoke['col']
        x, y = col, num_rows - row - 1  # Ajuste para coordenadas

        color = 'gray'
        marker_shape = 's'  # Cuadrado para representar el humo

        ax.scatter(x, y, marker=marker_shape, color=color, s=100)

def draw_fire(ax, fires, num_rows):
    """
    Dibuja los fuegos en el grid.

    Args:
        ax (matplotlib.axes.Axes): Eje de Matplotlib donde dibujar.
        fires (list of dict): Lista con información de los fuegos.
        num_rows (int): Número de filas del grid para ajustar coordenadas.
    """
    for fire in fires:
        row, col = fire['row'], fire['col']
        x, y = col, num_rows - row - 1  # Ajuste para coordenadas

        color = 'red'
        marker_shape = '*'  # Forma de estrella para representar el fuego

        ax.scatter(x, y, marker=marker_shape, color=color, s=200)

def draw_poi(ax, markers, num_rows):
    """
    Dibuja los puntos de interés (POI) con colores según su estado.
    - No revelado: cian
    - Revelado y 'v': verde
    - Revelado y 'f': morado
    """
    for marker in markers:
        row, col, type_, revealed = marker
        x, y = col, num_rows - row - 1 # Ajuste para coordenadas

        if revealed:
            continue
        else:
            if not revealed:
                color = 'cyan'
                marker_shape = 'o'
            elif type_ == 'v':
                color = 'green'
                marker_shape = 'o'
            elif type_ == 'f':
                color = 'magenta'
                marker_shape = 'x'


        ax.scatter(x, y, marker=marker_shape, color=color, s=100)

def draw_walls(ax, walls_grid, door_dict, entrances):
    # Dentro de la función draw_walls o después de crear el subplot
    for spine in ax.spines.values():
        spine.set_visible(False)

    num_rows = len(walls_grid)
    num_cols = len(walls_grid[0])

    # Crear un conjunto de entradas para acceso rápido
    entrances_set = set((entry['col'], entry['row']) for entry in entrances)

    for row in range(num_rows):
        for col in range(num_cols):
            walls = walls_grid[row][col]
            x, y = col - 0.5, num_rows - row - 0.5  # Ajuste para coordenadas

            cell = (col, row)  # Coordenadas en orden (x, y)

            # Definir direcciones y ajustes
            directions = [
                ((0, -1), [x, x + 1], [y, y]),         # Pared superior
                ((1, 0),  [x + 1, x + 1], [y - 1, y]), # Pared derecha
                ((0, 1),  [x, x + 1], [y - 1, y - 1]), # Pared inferior
                ((-1, 0), [x, x], [y - 1, y])          # Pared izquierda
            ]

            for idx, ((dx, dy), x_coords, y_coords) in enumerate(directions):
                neighbor_col = col + dx
                neighbor_row = row + dy
                neighbor = (neighbor_col, neighbor_row)

                # Verificar si la pared existe
                if walls[idx] == "1":
                    # Verificar si es una pared en el borde
                    is_edge_wall = False
                    if neighbor_col < 0 or neighbor_col >= num_cols or neighbor_row < 0 or neighbor_row >= num_rows:
                        is_edge_wall = True

                    # Determinar el color de la pared
                    if is_edge_wall and cell in entrances_set:
                        # Pared perimetral con una entrada: dibujar en naranja
                        color = "orange"
                        linestyle = "-"
                    else:
                        # Pared normal o perimetral sin entrada
                        door = door_dict.get((cell, neighbor))
                        if door:
                            if door['is_open']:
                                color = "green"
                                linestyle = "--"
                            else:
                                color = "brown"
                                linestyle = "-"
                        else:
                            color = "black"
                            linestyle = "-"

                    # Dibujar la pared con el color y estilo determinados
                    ax.plot(x_coords, y_coords, color=color, linestyle=linestyle, linewidth=2)
                else:
                    # No hay pared; no dibujamos nada
                    pass

WIDTH = 8
HEIGHT = 6

walls, markers, fire_markers, doors, entrances = parse_file("map.txt")

# Crear el diccionario de puertas
door_dict = {}
for door in doors:
    cell1 = (door['row1'], door['col1'])
    cell2 = (door['row2'], door['col2'])
    door_dict[(cell1, cell2)] = door
    door_dict[(cell2, cell1)] = door

model = BoardModel(WIDTH, HEIGHT, walls, doors, entrances, markers, fire_markers)
while model.steps <= 100:
    model.step()

print(f"Total de pasos: {model.steps}")

all_grids = model.datacollector.get_model_vars_dataframe()
all_grids.head(5)

fig, ax = plt.subplots(figsize=(8, 6))
custom_cmap = ListedColormap(["white", "green", "blue"])

# Dibujar paredes
# Definir el 'extent' para alinear los bordes de los píxeles con las coordenadas
extent = [-0.5, len(walls[0]) - 0.5, -0.5, len(walls) - 0.5]

def animate(i):
    ax.clear()
    ax.set_xticks([])
    ax.set_yticks([])

    # Obtener el estado de las puertas en el paso actual
    doors_state = all_grids.iloc[i]["Doors"]

    # Crear el door_dict para el cuadro actual
    door_dict_current = {}
    for door in doors_state:
        cell1 = (door['col1'], door['row1'])
        cell2 = (door['col2'], door['row2'])
        door_dict_current[(cell1, cell2)] = door
        door_dict_current[(cell2, cell1)] = door

    # Mostrar los agentes primero
    grid_state = all_grids.iloc[i]["Grid"]
    ax.imshow(grid_state, cmap=custom_cmap, interpolation="none", origin='upper', extent=extent)

    # Dibujar paredes y puertas con el estado actual, incluyendo las entradas
    draw_walls(ax, walls, door_dict_current, entrances)

    # Obtener el estado de los POIs en el paso actual
    poi_state = all_grids.iloc[i]["POI"]

    # Obtener el estado de los fuegos en el paso actual
    fire_state = all_grids.iloc[i]["Fires"]

    # Obtener el estado de los humos en el paso actual
    smoke_state = all_grids.iloc[i]["Smokes"]

    # Dibujar los POIs con colores según su estado y tipo
    num_rows = len(walls)  # Asumiendo que 'walls' es la lista de filas
    draw_poi(ax, poi_state, num_rows)

    # Dibujar los fuegos
    draw_fire(ax, fire_state, num_rows)

    # Dibujar los humos
    draw_smoke(ax, smoke_state, num_rows)

anim = animation.FuncAnimation(fig, animate, frames=model.steps)
anim