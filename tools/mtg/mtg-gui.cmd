@echo off
rem Start the MTG GUI (needs Python). Double click this file.
python "%~dp0gui.py"
if errorlevel 1 pause
