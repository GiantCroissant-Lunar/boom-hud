import yaml
import json
import sys
import os

def convert_openapi_to_schema(openapi_path, output_path):
    print(f"Reading {openapi_path}...")
    try:
        with open(openapi_path, 'r', encoding='utf-8') as f:
            openapi = yaml.safe_load(f)
    except Exception as e:
        print(f"Error reading YAML: {e}")
        return

    # Extract components/schemas
    schemas = openapi.get('components', {}).get('schemas', {})
    
    # We want to create a schema that validates the "GetFileResponse" structure
    # which is defined in paths -> /v1/files/{file_key} -> get -> responses -> 200 -> content -> application/json -> schema
    # OR we can look for it in components/responses if it is ref'd there.
    
    # Looking at the file content I read earlier:
    # "200": $ref: "#/components/responses/GetFileResponse"
    
    # And GetFileResponse is in components/responses (not schemas, but maybe responses section?)
    # Wait, the YAML showed:
    # components:
    #   responses:
    #     GetFileResponse: ...
    #       content:
    #         application/json:
    #           schema:
    #             type: object
    #             properties: ...
    
    # Let's try to find that schema.
    responses = openapi.get('components', {}).get('responses', {})
    get_file_response = responses.get('GetFileResponse', {})
    content = get_file_response.get('content', {}).get('application/json', {})
    root_schema = content.get('schema', {})

    if not root_schema:
        print("Could not find GetFileResponse schema. Falling back to simple generic wrapper around DocumentNode.")
        # Fallback: Create a schema that mimics the known structure if we can't find the exact one
        root_schema = {
            "type": "object",
            "properties": {
                "document": {"$ref": "#/definitions/DocumentNode"},
                "components": {"type": "object"},
                "styles": {"type": "object"},
                "name": {"type": "string"},
                "lastModified": {"type": "string"},
                "version": {"type": "string"}
            },
            "required": ["document"]
        }

    # Construct the final JSON Schema
    json_schema = {
        "$schema": "http://json-schema.org/draft-07/schema#",
        "title": "Figma REST API Export",
        "type": "object",
        # Copy the root schema properties
        **root_schema,
        # Definitions (schemas)
        "definitions": schemas
    }

    # OpenAPI 3.1 uses $ref: "#/components/schemas/..." 
    # JSON Schema usually wants "#/definitions/..."
    # We need to recursively fix refs.
    
    def fix_refs(node):
        if isinstance(node, dict):
            for k, v in node.items():
                if k == "$ref" and isinstance(v, str):
                    if v.startswith("#/components/schemas/"):
                        node[k] = v.replace("#/components/schemas/", "#/definitions/")
                    elif v.startswith("#/components/responses/"):
                         # This might be tricky if responses are not in definitions. 
                         # For now, let's assume mostly schemas are ref'd.
                         pass
                else:
                    fix_refs(v)
        elif isinstance(node, list):
            for item in node:
                fix_refs(item)

    fix_refs(json_schema)

    print(f"Writing {output_path}...")
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(json_schema, f, indent=2)
    print("Done.")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python convert_openapi.py <input_yaml> <output_json>")
    else:
        convert_openapi_to_schema(sys.argv[1], sys.argv[2])
