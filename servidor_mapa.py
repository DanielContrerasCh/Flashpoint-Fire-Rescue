from flask import Flask, jsonify
from flask_cors import CORS
import re

app = Flask(__name__)
CORS(app)

def parse_map_file(filename):
    with open(filename, 'r') as file:
        # Leer todas las líneas excepto la última (que es el comentario)
        lines = file.readlines()[:-1]
    
    # Inicializar el diccionario de respuesta
    map_data = {
        "cells": [],
        "pointsOfInterest": [],
        "firePositions": [],
        "doors": [],
        "entryPoints": []
    }
    
    current_line = 0
    
    # Parsear las celdas (6 líneas)
    for i in range(6):
        # Dividir la línea en grupos de 4 dígitos
        cell_row = re.findall(r'\d{4}', lines[current_line].strip())
        map_data["cells"].append(cell_row)
        current_line += 1
    
    # Parsear puntos de interés (3 líneas)
    for _ in range(3):
        row, col, poi_type = lines[current_line].strip().split()
        map_data["pointsOfInterest"].append({
            "row": int(row),
            "col": int(col),
            "type": poi_type
        })
        current_line += 1
    
    # Parsear posiciones de fuego (10 líneas)
    for _ in range(10):
        row, col = map(int, lines[current_line].strip().split())
        map_data["firePositions"].append({
            "x": row,
            "y": col
        })
        current_line += 1
    
    # Parsear puertas (8 líneas)
    for _ in range(8):
        r1, c1, r2, c2 = map(int, lines[current_line].strip().split())
        map_data["doors"].append({
            "r1": r1,
            "c1": c1,
            "r2": r2,
            "c2": c2
        })
        current_line += 1
    
    # Parsear puntos de entrada (4 líneas)
    for _ in range(4):
        row, col = map(int, lines[current_line].strip().split())
        map_data["entryPoints"].append({
            "x": row,
            "y": col
        })
        current_line += 1
    
    return map_data

@app.route('/api/map')
def get_map():
    try:
        map_data = parse_map_file('map.txt')
        return jsonify(map_data)
    except Exception as e:
        # Agregar más información de debugging
        import traceback
        error_info = {
            "error": str(e),
            "traceback": traceback.format_exc()
        }
        return jsonify(error_info), 500

@app.route('/api/map/validate', methods=['POST'])
def validate_map():
    try:
        map_data = parse_map_file('map.txt')
        validation = {
            "valid": True,
            "cells_count": len(map_data["cells"]),
            "poi_count": len(map_data["pointsOfInterest"]),
            "fire_positions_count": len(map_data["firePositions"]),
            "doors_count": len(map_data["doors"]),
            "entry_points_count": len(map_data["entryPoints"])
        }
        return jsonify(validation)
    except Exception as e:
        return jsonify({
            "valid": False,
            "error": str(e)
        }), 400

if __name__ == '__main__':
    app.run(debug=True, host='0.0.0.0', port=5000)