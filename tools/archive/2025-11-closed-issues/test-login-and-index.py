#!/usr/bin/env python3
"""Test login and chess indexing"""
import requests
import json
import sys

BASE_URL = "http://localhost:8080"

def test_login():
    """Test login endpoint"""
    print("Testing login...")
    response = requests.post(
        f"{BASE_URL}/auth/login",
        json={"email": "admin@meepleai.dev", "password": "Demo123!"},
        timeout=10
    )

    if response.status_code == 200:
        print(f"[OK] Login successful: {response.json()}")
        return response.cookies
    else:
        print(f"[FAIL] Login failed: {response.status_code} - {response.text}")
        return None

def test_chess_indexing(cookies):
    """Test chess knowledge indexing"""
    print("\nIndexing chess knowledge...")
    response = requests.post(
        f"{BASE_URL}/chess/index",
        cookies=cookies,
        timeout=180
    )

    if response.status_code == 200:
        data = response.json()
        print(f"[OK] Indexing successful:")
        print(f"  Total items: {data.get('totalItems', 0)}")
        print(f"  Total chunks: {data.get('totalChunks', 0)}")
        print(f"  Categories: {data.get('categoryCounts', {})}")
        return True
    else:
        print(f"[FAIL] Indexing failed: {response.status_code} - {response.text}")
        return False

def main():
    cookies = test_login()
    if cookies:
        test_chess_indexing(cookies)
    else:
        sys.exit(1)

if __name__ == "__main__":
    main()
