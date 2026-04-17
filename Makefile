.PHONY: docs

PYTHON      := python3
DOCS_SCRIPT := docs/tools/update-readme.py
DOCS_FILES  := $(wildcard docs/*.md)
DOCS_README_SRC    := $(filter-out docs/README.md,$(DOCS_FILES))
DOCS_README_TPL    := docs/templates/README-template.md

## docs/templates/README-template.md 또는 docs/*.md가 README.md보다 새로우면 스크립트 실행
docs/README.md: $(DOCS_README_SRC) $(DOCS_README_TPL)
	$(PYTHON) $(DOCS_SCRIPT)

## 진입점
docs: docs/README.md
