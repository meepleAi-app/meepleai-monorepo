#!/usr/bin/env python3
"""
MeepleAI - Google Drive Configuration Loader

Carica la configurazione del progetto da Google Drive usando l'API ufficiale.
Supporta cartelle private con autenticazione OAuth.

Prerequisiti:
    pip install google-api-python-client google-auth-oauthlib

Setup iniziale:
    1. Vai su https://console.cloud.google.com/apis/credentials
    2. Crea un progetto o seleziona esistente
    3. Abilita Google Drive API
    4. Crea credenziali OAuth 2.0 (Desktop App)
    5. Scarica il JSON e salvalo come credentials.json nella root del progetto

Uso:
    python scripts/gdrive/gdrive_config_loader.py [--folder-id FOLDER_ID]

Struttura attesa su Google Drive:
    MeepleAI/
    └── Config/
        ├── .env.local
        ├── .env.staging (opzionale)
        └── secrets/
            ├── postgres-password.txt
            ├── openrouter-api-key.txt
            └── ...
"""

import os
import sys
import io
import argparse
import stat
from pathlib import Path
from typing import Optional, List, Dict

# Verifica dipendenze
try:
    from google.oauth2.credentials import Credentials
    from google_auth_oauthlib.flow import InstalledAppFlow
    from google.auth.transport.requests import Request
    from googleapiclient.discovery import build
    from googleapiclient.http import MediaIoBaseDownload
    from googleapiclient.errors import HttpError
except ImportError:
    print("❌ Dipendenze mancanti. Installa con:")
    print("   pip install google-api-python-client google-auth-oauthlib")
    sys.exit(1)


# Costanti
SCOPES = ['https://www.googleapis.com/auth/drive.readonly']
PROJECT_ROOT = Path(__file__).parent.parent.parent
CREDENTIALS_FILE = PROJECT_ROOT / 'credentials.json'
TOKEN_FILE = PROJECT_ROOT / '.gdrive-token.json'
TEMP_DIR = PROJECT_ROOT / '.config-temp'

# Configurazione default (modifica se necessario)
DEFAULT_FOLDER_ID = 'YOUR_FOLDER_ID_HERE'


class Colors:
    """Colori ANSI per output."""
    RED = '\033[0;31m'
    GREEN = '\033[0;32m'
    YELLOW = '\033[1;33m'
    BLUE = '\033[0;34m'
    NC = '\033[0m'


def print_header():
    """Stampa header."""
    print(f"{Colors.BLUE}╔════════════════════════════════════════════════════════════╗{Colors.NC}")
    print(f"{Colors.BLUE}║   MeepleAI - Google Drive Configuration Loader (Python)   ║{Colors.NC}")
    print(f"{Colors.BLUE}╚════════════════════════════════════════════════════════════╝{Colors.NC}")
    print()


def authenticate() -> Optional[Credentials]:
    """
    Autentica con Google Drive API.

    Returns:
        Credentials valide o None se fallisce.
    """
    creds = None

    # Carica token esistente
    if TOKEN_FILE.exists():
        creds = Credentials.from_authorized_user_file(str(TOKEN_FILE), SCOPES)

    # Se non valido, rinnova o richiedi
    if not creds or not creds.valid:
        if creds and creds.expired and creds.refresh_token:
            try:
                print(f"{Colors.YELLOW}🔄 Rinnovando token...{Colors.NC}")
                creds.refresh(Request())
            except Exception as e:
                print(f"{Colors.RED}❌ Errore refresh token: {e}{Colors.NC}")
                creds = None

        if not creds:
            # Verifica credentials.json
            if not CREDENTIALS_FILE.exists():
                print(f"{Colors.RED}❌ File credentials.json non trovato!{Colors.NC}")
                print()
                print("Per ottenere credentials.json:")
                print("  1. Vai su https://console.cloud.google.com/apis/credentials")
                print("  2. Crea un progetto o seleziona esistente")
                print("  3. Abilita Google Drive API")
                print("  4. Crea credenziali OAuth 2.0 (Desktop App)")
                print("  5. Scarica il JSON e salvalo come credentials.json")
                return None

            print(f"{Colors.YELLOW}🔐 Richiesta autorizzazione...{Colors.NC}")
            print("    Si aprirà il browser per l'autenticazione Google.")
            print()

            try:
                flow = InstalledAppFlow.from_client_secrets_file(
                    str(CREDENTIALS_FILE), SCOPES)
                creds = flow.run_local_server(port=0)
            except Exception as e:
                print(f"{Colors.RED}❌ Errore autenticazione: {e}{Colors.NC}")
                return None

        # Salva token
        if creds:
            with open(TOKEN_FILE, 'w') as token:
                token.write(creds.to_json())
            print(f"{Colors.GREEN}✅ Token salvato in {TOKEN_FILE.name}{Colors.NC}")

    return creds


def list_folder_contents(service, folder_id: str) -> List[Dict]:
    """
    Lista contenuti di una cartella Google Drive.

    Args:
        service: Google Drive service
        folder_id: ID della cartella

    Returns:
        Lista di file/cartelle con id, name, mimeType
    """
    try:
        query = f"'{folder_id}' in parents and trashed = false"
        results = service.files().list(
            q=query,
            fields="files(id, name, mimeType, size)"
        ).execute()
        return results.get('files', [])
    except HttpError as e:
        print(f"{Colors.RED}❌ Errore listing cartella: {e}{Colors.NC}")
        return []


def download_file(service, file_id: str, dest_path: Path) -> bool:
    """
    Scarica un file da Google Drive.

    Args:
        service: Google Drive service
        file_id: ID del file
        dest_path: Path destinazione

    Returns:
        True se successo
    """
    try:
        request = service.files().get_media(fileId=file_id)
        fh = io.BytesIO()
        downloader = MediaIoBaseDownload(fh, request)

        done = False
        while not done:
            status, done = downloader.next_chunk()

        dest_path.parent.mkdir(parents=True, exist_ok=True)
        with open(dest_path, 'wb') as f:
            f.write(fh.getvalue())

        return True
    except HttpError as e:
        print(f"{Colors.RED}  ❌ Errore download {dest_path.name}: {e}{Colors.NC}")
        return False


def download_folder_recursive(service, folder_id: str, dest_dir: Path, indent: str = "") -> int:
    """
    Scarica ricorsivamente una cartella da Google Drive.

    Args:
        service: Google Drive service
        folder_id: ID della cartella
        dest_dir: Directory destinazione
        indent: Indentazione per output

    Returns:
        Numero di file scaricati
    """
    files = list_folder_contents(service, folder_id)
    downloaded = 0

    for file in files:
        name = file['name']
        file_id = file['id']
        mime_type = file['mimeType']

        if mime_type == 'application/vnd.google-apps.folder':
            # Ricorsione per sottocartelle
            print(f"{indent}{Colors.BLUE}📁 {name}/{Colors.NC}")
            sub_dir = dest_dir / name
            downloaded += download_folder_recursive(service, file_id, sub_dir, indent + "  ")
        else:
            # Download file
            dest_path = dest_dir / name
            if download_file(service, file_id, dest_path):
                size = int(file.get('size', 0))
                size_str = f"({size:,} bytes)" if size else ""
                print(f"{indent}{Colors.GREEN}✓ {name}{Colors.NC} {size_str}")
                downloaded += 1
            else:
                print(f"{indent}{Colors.RED}✗ {name}{Colors.NC}")

    return downloaded


def apply_configuration(temp_dir: Path) -> Dict[str, bool]:
    """
    Applica i file scaricati alle posizioni corrette.

    Args:
        temp_dir: Directory con file scaricati

    Returns:
        Dict con stato applicazione per ogni file
    """
    results = {}

    # Trova directory principale (potrebbe essere una sottocartella)
    config_dirs = list(temp_dir.iterdir())
    config_dir = config_dirs[0] if config_dirs and config_dirs[0].is_dir() else temp_dir

    print(f"{Colors.YELLOW}🔧 Applicando configurazione...{Colors.NC}")

    # .env.local
    env_local = config_dir / '.env.local'
    if env_local.exists():
        dest = PROJECT_ROOT / '.env.local'

        # Backup se esiste
        if dest.exists():
            from datetime import datetime
            backup = PROJECT_ROOT / f'.env.local.backup.{datetime.now().strftime("%Y%m%d_%H%M%S")}'
            dest.rename(backup)
            print(f"  {Colors.YELLOW}⚠️  Backup creato: {backup.name}{Colors.NC}")

        env_local.rename(dest)
        results['.env.local'] = True
        print(f"  {Colors.GREEN}✅ .env.local installato{Colors.NC}")
    else:
        results['.env.local'] = False
        print(f"  {Colors.YELLOW}⚠️  .env.local non trovato{Colors.NC}")

    # .env.staging (opzionale)
    env_staging = config_dir / '.env.staging'
    if env_staging.exists():
        dest = PROJECT_ROOT / '.env.staging'
        env_staging.rename(dest)
        results['.env.staging'] = True
        print(f"  {Colors.GREEN}✅ .env.staging installato{Colors.NC}")

    # Secrets
    secrets_dir = config_dir / 'secrets'
    if secrets_dir.exists() and secrets_dir.is_dir():
        dest_secrets = PROJECT_ROOT / 'infra' / 'secrets'
        dest_secrets.mkdir(parents=True, exist_ok=True)

        secret_count = 0
        for secret_file in secrets_dir.glob('*.txt'):
            dest = dest_secrets / secret_file.name
            secret_file.rename(dest)
            # Imposta permessi sicuri (600)
            os.chmod(dest, stat.S_IRUSR | stat.S_IWUSR)
            secret_count += 1

        results['secrets'] = secret_count > 0
        print(f"  {Colors.GREEN}✅ {secret_count} secrets installati con permessi sicuri{Colors.NC}")
    else:
        results['secrets'] = False
        print(f"  {Colors.YELLOW}⚠️  Directory secrets non trovata{Colors.NC}")

    return results


def validate_installation() -> bool:
    """
    Valida l'installazione della configurazione.

    Returns:
        True se la configurazione è valida
    """
    print(f"{Colors.YELLOW}🔍 Validando installazione...{Colors.NC}")
    errors = 0

    env_local = PROJECT_ROOT / '.env.local'
    if env_local.exists():
        print(f"  {Colors.GREEN}✓ .env.local presente{Colors.NC}")

        content = env_local.read_text()

        # Verifica OPENROUTER_API_KEY
        if 'OPENROUTER_API_KEY=sk-' in content:
            print(f"  {Colors.GREEN}✓ OPENROUTER_API_KEY configurato{Colors.NC}")
        else:
            print(f"  {Colors.RED}✗ OPENROUTER_API_KEY mancante o vuoto{Colors.NC}")
            errors += 1

        # Verifica POSTGRES_PASSWORD
        if 'POSTGRES_PASSWORD=' in content and 'POSTGRES_PASSWORD=\n' not in content:
            print(f"  {Colors.GREEN}✓ POSTGRES_PASSWORD presente{Colors.NC}")
        else:
            print(f"  {Colors.RED}✗ POSTGRES_PASSWORD mancante{Colors.NC}")
            errors += 1
    else:
        print(f"  {Colors.RED}✗ .env.local non trovato{Colors.NC}")
        errors += 1

    # Verifica secrets
    secrets_dir = PROJECT_ROOT / 'infra' / 'secrets'
    if secrets_dir.exists():
        secret_files = list(secrets_dir.glob('*.txt'))
        if secret_files:
            print(f"  {Colors.GREEN}✓ {len(secret_files)} secrets installati{Colors.NC}")
        else:
            print(f"  {Colors.YELLOW}⚠ Nessun secret trovato{Colors.NC}")

    if errors > 0:
        print(f"{Colors.RED}⚠️  Trovati {errors} problemi di configurazione{Colors.NC}")
        return False

    print(f"{Colors.GREEN}✅ Validazione completata con successo{Colors.NC}")
    return True


def cleanup():
    """Pulisce i file temporanei."""
    import shutil
    if TEMP_DIR.exists():
        shutil.rmtree(TEMP_DIR)
        print(f"{Colors.GREEN}✅ File temporanei rimossi{Colors.NC}")


def main():
    """Entry point principale."""
    parser = argparse.ArgumentParser(
        description='Carica configurazione MeepleAI da Google Drive'
    )
    parser.add_argument(
        '--folder-id', '-f',
        default=DEFAULT_FOLDER_ID,
        help='ID della cartella Google Drive'
    )
    parser.add_argument(
        '--no-validate',
        action='store_true',
        help='Salta validazione finale'
    )
    args = parser.parse_args()

    print_header()

    # Verifica folder ID
    if args.folder_id == 'YOUR_FOLDER_ID_HERE':
        print(f"{Colors.RED}❌ FOLDER_ID non configurato!{Colors.NC}")
        print()
        print("Passa il FOLDER_ID come argomento:")
        print(f"  {Colors.YELLOW}python {__file__} --folder-id 1ABC123xyz{Colors.NC}")
        print()
        print("Per ottenere il FOLDER_ID:")
        print("  1. Apri la cartella su Google Drive")
        print("  2. L'URL sarà: https://drive.google.com/drive/folders/FOLDER_ID")
        print("  3. Copia l'ID dalla URL")
        sys.exit(1)

    # Autenticazione
    creds = authenticate()
    if not creds:
        sys.exit(1)

    print(f"{Colors.GREEN}✅ Autenticato con Google Drive{Colors.NC}")
    print()

    # Crea servizio
    try:
        service = build('drive', 'v3', credentials=creds)
    except Exception as e:
        print(f"{Colors.RED}❌ Errore creazione servizio: {e}{Colors.NC}")
        sys.exit(1)

    # Download
    print(f"{Colors.YELLOW}📥 Scaricando da Google Drive...{Colors.NC}")
    TEMP_DIR.mkdir(parents=True, exist_ok=True)

    downloaded = download_folder_recursive(service, args.folder_id, TEMP_DIR)
    print()
    print(f"{Colors.GREEN}✅ Scaricati {downloaded} file{Colors.NC}")
    print()

    if downloaded == 0:
        print(f"{Colors.RED}❌ Nessun file scaricato. Verifica il FOLDER_ID.{Colors.NC}")
        cleanup()
        sys.exit(1)

    # Applica configurazione
    apply_configuration(TEMP_DIR)
    print()

    # Cleanup
    cleanup()
    print()

    # Validazione
    if not args.no_validate:
        if validate_installation():
            print()
            print(f"{Colors.GREEN}🎉 Configurazione caricata con successo!{Colors.NC}")
            print()
            print(f"{Colors.YELLOW}Prossimi passi:{Colors.NC}")
            print(f"  1. Verifica .env.local: {Colors.BLUE}cat .env.local | head -20{Colors.NC}")
            print(f"  2. Avvia i servizi: {Colors.BLUE}cd infra && docker compose up -d{Colors.NC}")
            print(f"  3. Verifica stato: {Colors.BLUE}docker compose ps{Colors.NC}")
            sys.exit(0)
        else:
            print()
            print(f"{Colors.RED}❌ Configurazione incompleta. Verifica manualmente.{Colors.NC}")
            sys.exit(1)
    else:
        print(f"{Colors.GREEN}🎉 Download completato (validazione saltata){Colors.NC}")


if __name__ == '__main__':
    main()
