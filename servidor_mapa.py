from flask import Flask, jsonify
from flask_cors import CORS
import re

app = Flask(__name__)
CORS(app)

def parse_map_file(filename):
    with open(filename, 'r') as file:
        lines = file.readlines()[:]
    
    map_data = {
        "cells": [],
        "pointsOfInterest": [],
        "firePositions": [],
        "doors": [],
        "entryPoints": []
    }
    
    current_line = 0
    
    for i in range(6):
        cell_row = re.findall(r'\d{4}', lines[current_line].strip())
        map_data["cells"].append(cell_row)
        current_line += 1
    
    for _ in range(3):
        row, col, poi_type = lines[current_line].strip().split()
        map_data["pointsOfInterest"].append({
            "row": int(row),
            "col": int(col),
            "type": poi_type
        })
        current_line += 1
    
    for _ in range(10):
        row, col = map(int, lines[current_line].strip().split())
        map_data["firePositions"].append({
            "x": row,
            "y": col
        })
        current_line += 1
    
    for _ in range(8):
        r1, c1, r2, c2 = map(int, lines[current_line].strip().split())
        map_data["doors"].append({
            "r1": r1,
            "c1": c1,
            "r2": r2,
            "c2": c2
        })
        current_line += 1
    
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
        map_data = parse_map_file('final.txt')
        return jsonify(map_data)
    except Exception as e:
        import traceback
        error_info = {
            "error": str(e),
            "traceback": traceback.format_exc()
        }
        return jsonify(error_info), 500
    
@app.route('/api/simulation', methods=['GET'])
def run_simulation():
    from AgentesModelo import BoardModel, parse_file
    
    try:
        walls, markers, fire_markers, doors, entrances = parse_file('final.txt')
        model = BoardModel(6, 8, walls, doors, entrances, markers, fire_markers)
        
        simulation_results = []
        
        while not model.check_termination_conditions():
            model.step()
            grid_state = model.datacollector.get_model_vars_dataframe().iloc[-1].to_dict()
            simulation_results.append({
                "grid": grid_state["Grid"].tolist(),
                "fires": grid_state["Fires"],
                "smokes": grid_state["Smokes"],
                "agents": [{"id": agent.unique_id, "pos": agent.pos} for agent in model.schedule.agents],
                "rescued_victims": model.rescued_victims,
                "total_damage": model.total_damage
            })
        
        return jsonify(simulation_results)
    except Exception as e:
        import traceback
        return jsonify({
            "error": str(e),
            "traceback": traceback.format_exc()
        }), 500


if __name__ == '__main__':
    app.run(debug=True, host='0.0.0.0', port=5000)