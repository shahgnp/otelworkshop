.PHONY: lab1-up lab1-down lab2-up lab2-down logs-app logs-collector counters load reset prepull

COMPOSE = docker compose

lab1-up:
	LAB=starter LOG_FORMATTER=$${LOG_FORMATTER:-simple} $(COMPOSE) up --build -d app

lab1-down:
	$(COMPOSE) down

lab2-up:
	LAB=lab2-solution $(COMPOSE) --profile collector up --build -d app collector

lab2-down:
	$(COMPOSE) --profile collector down

logs-app:
	$(COMPOSE) logs --follow app

logs-collector:
	$(COMPOSE) --profile collector logs --follow collector

counters:
	LAB=starter $(COMPOSE) --profile tools run --rm tools monitor --process-id 1 System.Runtime Microsoft.AspNetCore.Hosting Workshop.Orders

load:
	$(COMPOSE) --profile load run --rm --no-deps load

reset:
	$(COMPOSE) --profile collector --profile tools --profile load down --volumes --remove-orphans

prepull:
	./scripts/prepull.sh